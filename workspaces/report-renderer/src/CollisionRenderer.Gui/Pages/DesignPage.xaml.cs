using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using CollisionRenderer.Core;
using CollisionRenderer.Gui;
using CollisionRenderer.Gui.Models;
using CollisionRenderer.Gui.Services;
using CollisionRenderer.Gui.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;

namespace CollisionRenderer.Gui.Pages;

/// <summary>
/// The design surface for one chosen template: a form built from the Core form
/// definition, a JSON escape hatch, and a live HTML preview that updates as the user
/// types. "Render PDF" produces the final paginated document via the shared Core engine.
/// </summary>
public sealed partial class DesignPage : Page
{
    public DesignViewModel ViewModel { get; } = new();

    private readonly IPreviewComposer _previewComposer = CollisionRendererFactory.CreatePreviewComposer();
    private readonly DesktopStateService _state = new();
    private readonly DispatcherQueueTimer _previewTimer;
    private readonly DispatcherQueueTimer _autosaveTimer;

    private string _authoringId = string.Empty;
    private string? _previewTempPath;
    private string? _htmlPreviewTempPath;
    private string? _lastGoodHtml;
    private string? _pendingAutosave;
    private JsonNode? _draftRoot;
    private bool _refreshingForm;
    private bool _loading;
    private bool _dirty;
    private bool _forceClose;
    private bool _navigatedAway;
    private bool _dialogOpen;
    private Dictionary<string, List<string>> _fieldErrors = new(StringComparer.OrdinalIgnoreCase);

    public DesignPage()
    {
        InitializeComponent();

        _previewTimer = DispatcherQueue.CreateTimer();
        _previewTimer.Interval = TimeSpan.FromMilliseconds(350);
        _previewTimer.IsRepeating = false;
        _previewTimer.Tick += (_, _) => RefreshPreview();

        _autosaveTimer = DispatcherQueue.CreateTimer();
        _autosaveTimer.Interval = TimeSpan.FromSeconds(4);
        _autosaveTimer.IsRepeating = false;
        _autosaveTimer.Tick += (_, _) =>
        {
            // Snapshot the values and write off the UI thread so a slow disk can't jank typing.
            var id = _authoringId;
            var json = ViewModel.PayloadJson ?? string.Empty;
            _ = Task.Run(() => _state.SaveAutosave(id, json));
        };

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    // --------------------------------------------------------------- navigation

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _navigatedAway = false;

        if (e.Parameter is not DesignNavArgs args)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }

            return;
        }

        _authoringId = args.AuthoringTemplateId;
        ViewModel.Initialize(args.AuthoringTemplateId);

        _loading = true;
        if (!string.IsNullOrEmpty(args.RestoreJson))
        {
            ViewModel.PayloadJson = args.RestoreJson!;
            ViewModel.StatusText = $"Reopened {ViewModel.SelectedTemplate?.Name}.";
        }
        else
        {
            ViewModel.LoadStarter();

            var autosave = _state.LoadAutosave(_authoringId);
            if (!string.IsNullOrWhiteSpace(autosave) &&
                !string.Equals(autosave, ViewModel.PayloadJson, StringComparison.Ordinal))
            {
                _pendingAutosave = autosave;
                AutosaveBar.IsOpen = true;
            }
        }

        _fieldErrors.Clear();
        BuildFormFromDraft();
        _loading = false;
        _dirty = false;

        if (App.Window is not null)
        {
            App.Window.AppWindow.Closing += OnWindowClosing;
        }

        SchedulePreview();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _navigatedAway = true;

        _previewTimer.Stop();
        _autosaveTimer.Stop();

        if (App.Window is not null)
        {
            App.Window.AppWindow.Closing -= OnWindowClosing;
        }

        TryDeleteTemp(_htmlPreviewTempPath);
        TryDeleteTemp(_previewTempPath);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignViewModel.PayloadJson))
        {
            SchedulePreview();
            if (!_loading)
            {
                _dirty = true;
                _autosaveTimer.Stop();
                _autosaveTimer.Start();
            }
        }
        else if (e.PropertyName == nameof(DesignViewModel.SelectedDensity))
        {
            SchedulePreview();
        }
    }

    private async void OnBackClick(object sender, RoutedEventArgs e) => await TryGoBackAsync();

    private async Task TryGoBackAsync()
    {
        if (_dirty && !await ConfirmDiscardAsync("Discard changes?"))
        {
            return;
        }

        _autosaveTimer.Stop();
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (!_dirty || _forceClose)
        {
            return;
        }

        e.Cancel = true;
        if (await ConfirmDiscardAsync("Close without saving?"))
        {
            _forceClose = true;
            App.Window?.Close();
        }
    }

    // --------------------------------------------------------------- live preview

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private async void RefreshPreview()
    {
        if (_navigatedAway || ViewModel.SelectedTemplate is null)
        {
            return;
        }

        var density = (ViewModel.SelectedDensity ?? DensityOption.All[0]).Density;

        PreviewResult result;
        try
        {
            result = _previewComposer.ComposePreview(
                ViewModel.SelectedTemplate.RenderTemplateId, ViewModel.PayloadJson ?? string.Empty, density);
        }
        catch
        {
            return;
        }

        if (!result.IsBestEffort)
        {
            _lastGoodHtml = result.Html;
            await ShowHtmlPreviewAsync(result.Html);
        }
        else if (_lastGoodHtml is null)
        {
            // Nothing good to fall back to yet — show the friendly placeholder page.
            await ShowHtmlPreviewAsync(result.Html);
        }

        // Otherwise keep the last good render on a transient bad keystroke.
    }

    private async Task ShowHtmlPreviewAsync(string html)
    {
        try
        {
            await HtmlPreview.EnsureCoreWebView2Async();

            // Write each refresh to a fresh file: overwriting the path WebView2 last
            // navigated to can hit a sharing lock while it is still loading that file.
            var path = Path.Combine(Path.GetTempPath(), $"cr_livepreview_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(path, html);
            HtmlPreview.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);

            var previous = _htmlPreviewTempPath;
            _htmlPreviewTempPath = path;
            TryDeleteTemp(previous);
        }
        catch
        {
            // The live preview is best-effort; a transient WebView2/IO failure is non-fatal.
        }
    }

    private void OnPreviewModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (HtmlPreview is null || PdfPreviewHost is null)
        {
            return;
        }

        var pdf = ReferenceEquals(sender.SelectedItem, PdfTab);
        HtmlPreview.Visibility = pdf ? Visibility.Collapsed : Visibility.Visible;
        PdfPreviewHost.Visibility = pdf ? Visibility.Visible : Visibility.Collapsed;
    }

    // --------------------------------------------------------------- view switching

    private void OnViewSwitcherSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ReferenceEquals(sender.SelectedItem, JsonTab))
        {
            FormView.Visibility = Visibility.Collapsed;
            JsonView.Visibility = Visibility.Visible;
            return;
        }

        BuildFormFromDraft();
        FormView.Visibility = Visibility.Visible;
        JsonView.Visibility = Visibility.Collapsed;
    }

    // --------------------------------------------------------------- data commands

    private void OnNewStarterClick(object sender, RoutedEventArgs e) => ResetDraft(ViewModel.LoadStarter);

    private void OnNewBlankClick(object sender, RoutedEventArgs e) => ResetDraft(ViewModel.LoadBlank);


    private void ResetDraft(Action load)
    {
        _fieldErrors.Clear();
        load();
        BuildFormFromDraft();
        ShowEditView();
    }

    private void OnRestoreAutosaveClick(object sender, RoutedEventArgs e)
    {
        if (_pendingAutosave is not null)
        {
            _loading = true;
            ViewModel.PayloadJson = _pendingAutosave;
            _loading = false;
            _dirty = true;
            _fieldErrors.Clear();
            BuildFormFromDraft();
        }

        _pendingAutosave = null;
        AutosaveBar.IsOpen = false;
    }

    private async void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker(App.WindowId)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".json");
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            ViewModel.PayloadJson = await File.ReadAllTextAsync(file.Path);
            ViewModel.StatusText = $"Loaded {Path.GetFileName(file.Path)}.";
            _fieldErrors.Clear();
            BuildFormFromDraft();
            ShowEditView();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Could not open file", ex.Message);
        }
    }

    private async void OnSaveDraftClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker(App.WindowId)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"{ViewModel.SelectedTemplate?.Id ?? "document"}_draft",
            };
            picker.FileTypeChoices.Add("JSON draft", new List<string> { ".json" });

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            await File.WriteAllTextAsync(file.Path, ViewModel.PayloadJson);
            ViewModel.StatusText = $"Saved draft {Path.GetFileName(file.Path)}.";
            CheckpointDraft();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Could not save draft", ex.Message);
        }
    }

    private async void OnBatchRenderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var manifestPicker = new FileOpenPicker(App.WindowId)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            manifestPicker.FileTypeFilter.Add(".json");

            var manifestFile = await manifestPicker.PickSingleFileAsync();
            if (manifestFile is null)
            {
                return;
            }

            var outputPicker = new FolderPicker(App.WindowId)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };

            var outputFolder = await outputPicker.PickSingleFolderAsync();
            if (outputFolder is null)
            {
                return;
            }

            BeginBusy("Batch rendering documents…");
            var summary = await RenderBatchAsync(manifestFile.Path, outputFolder.Path);
            ViewModel.StatusText = summary;
            await ShowMessageAsync("Batch render complete", summary);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Batch render failed", ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    // --------------------------------------------------------------- generated form

    private void BuildFormFromDraft()
    {
        if (ViewModel.SelectedTemplate is null || GeneratedForm is null)
        {
            return;
        }

        _refreshingForm = true;
        try
        {
            _draftRoot = ParseDraft(ViewModel.PayloadJson);
            GeneratedForm.Children.Clear();

            var form = CollisionRendererFactory.AuthoringCatalog.GetForm(ViewModel.SelectedTemplate.Id);
            foreach (var section in form.Sections)
            {
                GeneratedForm.Children.Add(BuildSection(section));
            }
        }
        catch (Exception ex)
        {
            GeneratedForm.Children.Clear();
            GeneratedForm.Children.Add(new TextBlock
            {
                Text = $"The form could not be built from the current draft: {ex.Message}",
                TextWrapping = TextWrapping.Wrap,
            });
        }
        finally
        {
            _refreshingForm = false;
        }
    }

    private FrameworkElement BuildSection(DocumentFormSection section)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = section.Title,
            Style = StyleResource("BrandSectionLabelStyle"),
        });

        if (!string.IsNullOrWhiteSpace(section.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = section.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushResource("BrandMutedBrush"),
                Style = StyleResource("CaptionTextBlockStyle"),
            });
        }

        foreach (var field in section.Fields)
        {
            panel.Children.Add(BuildField(field, field.Path));
        }

        return new Border
        {
            BorderBrush = BrushResource("BrandHairlineBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 14),
            Child = panel,
        };
    }

    private FrameworkElement BuildField(DocumentFormField field, string path)
    {
        var control = field.Kind switch
        {
            FormFieldKind.MultilineText => BuildTextField(field, path, multiline: true),
            FormFieldKind.Text or FormFieldKind.Date or FormFieldKind.Money or FormFieldKind.Number => BuildTextField(field, path),
            FormFieldKind.Checkbox => BuildCheckbox(field, path),
            FormFieldKind.Select or FormFieldKind.SignatureSelect => BuildSelect(field, path),
            FormFieldKind.ImageUpload => BuildUpload(field, path, images: true),
            FormFieldKind.PdfUpload => BuildUpload(field, path, images: false),
            FormFieldKind.Table => BuildTable(field, path),
            FormFieldKind.QuestionAnswer => BuildQuestionAnswer(field, path),
            FormFieldKind.Repeater => BuildRepeater(field, path),
            _ => BuildTextField(field, path),
        };

        return WithFieldErrors(path, control);
    }

    private FrameworkElement WithFieldErrors(string path, FrameworkElement control)
    {
        if (!_fieldErrors.TryGetValue(path, out var errors) || errors.Count == 0)
        {
            return control;
        }

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(control);
        foreach (var error in errors)
        {
            panel.Children.Add(new TextBlock
            {
                Text = error,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushResource("BrandRedBrush"),
                Style = StyleResource("CaptionTextBlockStyle"),
            });
        }

        return panel;
    }

    private FrameworkElement BuildTextField(DocumentFormField field, string path, bool multiline = false)
    {
        var box = new TextBox
        {
            Text = GetString(path),
            PlaceholderText = field.Placeholder ?? "",
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 92 : 0,
            Header = Label(field),
        };
        AutomationProperties.SetAutomationId(box, $"Field_{field.Id}_{SanitizeAutomationPath(path)}");

        box.TextChanged += (_, _) =>
        {
            if (_refreshingForm)
            {
                return;
            }

            SetValue(path, box.Text);
        };

        return box;
    }

    private FrameworkElement BuildCheckbox(DocumentFormField field, string path)
    {
        var check = new CheckBox
        {
            Content = Label(field),
            IsChecked = GetBool(path),
        };
        AutomationProperties.SetAutomationId(check, $"Field_{field.Id}_{SanitizeAutomationPath(path)}");
        check.Checked += (_, _) => SetValue(path, true);
        check.Unchecked += (_, _) => SetValue(path, false);
        return check;
    }

    private FrameworkElement BuildSelect(DocumentFormField field, string path)
    {
        var combo = new ComboBox
        {
            Header = Label(field),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetAutomationId(combo, $"Field_{field.Id}_{SanitizeAutomationPath(path)}");

        foreach (var option in field.Options)
        {
            combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
        }

        var current = GetString(path);
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), current, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                break;
            }
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item)
            {
                SetValue(path, item.Tag?.ToString() ?? "");
                ApplySignatureDefaults(path, item.Tag?.ToString());
            }
        };

        return combo;
    }

    private FrameworkElement BuildUpload(DocumentFormField field, string path, bool images)
    {
        var selected = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrushResource("BrandMutedBrush"),
            Style = StyleResource("CaptionTextBlockStyle"),
        };
        var preview = images
            ? new Image
            {
                MaxHeight = 96,
                HorizontalAlignment = HorizontalAlignment.Left,
                Stretch = Stretch.Uniform,
                Visibility = Visibility.Collapsed,
            }
            : null;
        UpdateUploadDisplay(selected, preview, GetString(path));

        var policy = ViewModel.SelectedTemplate?.AttachmentPolicy;

        var choose = new Button
        {
            Content = images ? "Choose image..." : "Choose PDF...",
            Style = StyleResource("BrandSecondaryButtonStyle"),
        };
        AutomationProperties.SetAutomationId(choose, $"Upload_{field.Id}_{SanitizeAutomationPath(path)}");

        var error = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrushResource("BrandRedBrush"),
            Style = StyleResource("CaptionTextBlockStyle"),
            Visibility = Visibility.Collapsed,
        };

        choose.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker(App.WindowId)
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            };

            if (images)
            {
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".webp");
            }
            else
            {
                picker.FileTypeFilter.Add(".pdf");
            }

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            if (!ValidateAttachment(file.Path, policy, out var message))
            {
                error.Text = message;
                error.Visibility = Visibility.Visible;
                return;
            }

            error.Visibility = Visibility.Collapsed;
            SetValue(path, file.Path);
            UpdateUploadDisplay(selected, preview, file.Path);
        };

        var clear = new Button
        {
            Content = "Clear",
            Style = StyleResource("BrandSecondaryButtonStyle"),
        };
        AutomationProperties.SetAutomationId(clear, $"Clear_{field.Id}_{SanitizeAutomationPath(path)}");
        clear.Click += (_, _) =>
        {
            error.Visibility = Visibility.Collapsed;
            SetValue(path, "");
            UpdateUploadDisplay(selected, preview, "");
        };

        var row = new StackPanel { Spacing = 6 };
        row.Children.Add(new TextBlock
        {
            Text = Label(field),
            Style = StyleResource("CaptionTextBlockStyle"),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(choose);
        buttons.Children.Add(clear);
        row.Children.Add(buttons);
        if (preview is not null)
        {
            row.Children.Add(preview);
        }

        row.Children.Add(selected);
        row.Children.Add(error);
        return row;
    }

    private static bool ValidateAttachment(string path, AttachmentPolicy? policy, out string message)
    {
        message = string.Empty;
        try
        {
            var info = new FileInfo(path);
            var max = policy?.MaxAttachmentBytes ?? 15_000_000;
            if (info.Exists && info.Length > max)
            {
                message = $"That file is {info.Length:N0} bytes, above the {max:N0}-byte limit for this document.";
                return false;
            }
        }
        catch
        {
            // If the size can't be read, let the renderer's validation catch it.
        }

        return true;
    }

    private FrameworkElement BuildTable(DocumentFormField field, string path)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = Label(field), Style = StyleResource("CaptionTextBlockStyle") });

        var rows = GetArray(path);
        for (var i = 0; i < rows.Count; i++)
        {
            var rowPath = $"{path}[{i}]";
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBox { Text = GetString($"{rowPath}.label"), PlaceholderText = "Label" };
            label.TextChanged += (_, _) => SetValue($"{rowPath}.label", label.Text);
            var value = new TextBox { Text = GetString($"{rowPath}.value"), PlaceholderText = "Value" };
            value.TextChanged += (_, _) => SetValue($"{rowPath}.value", value.Text);

            Grid.SetColumn(label, 0);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
            panel.Children.Add(grid);
        }

        var add = new Button { Content = "Add row", Style = StyleResource("BrandSecondaryButtonStyle") };
        AutomationProperties.SetAutomationId(add, $"AddRow_{field.Id}_{SanitizeAutomationPath(path)}");
        add.Click += (_, _) =>
        {
            GetOrCreateArray(path).Add(new JsonObject { ["label"] = "", ["value"] = "" });
            UpdatePayloadText();
            BuildFormFromDraft();
        };
        panel.Children.Add(add);

        return panel;
    }

    private FrameworkElement BuildQuestionAnswer(DocumentFormField field, string path)
    {
        var wrapper = new StackPanel { Spacing = 10 };
        wrapper.Children.Add(new TextBlock { Text = Label(field), Style = StyleResource("CaptionTextBlockStyle") });
        var rows = GetArray(path);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowPath = $"{path}[{i}]";
            wrapper.Children.Add(BuildTextField(new DocumentFormField { Id = "question", Label = $"Question {i + 1}", Kind = FormFieldKind.MultilineText, Path = $"{rowPath}[0]" }, $"{rowPath}[0]", multiline: true));
            wrapper.Children.Add(BuildTextField(new DocumentFormField { Id = "response", Label = $"Response {i + 1}", Kind = FormFieldKind.MultilineText, Path = $"{rowPath}[1]" }, $"{rowPath}[1]", multiline: true));
        }

        var add = new Button { Content = "Add question", Style = StyleResource("BrandSecondaryButtonStyle") };
        AutomationProperties.SetAutomationId(add, $"AddQuestion_{field.Id}_{SanitizeAutomationPath(path)}");
        add.Click += (_, _) =>
        {
            GetOrCreateArray(path).Add(new JsonArray("", ""));
            UpdatePayloadText();
            BuildFormFromDraft();
        };
        wrapper.Children.Add(add);
        return wrapper;
    }

    private FrameworkElement BuildRepeater(DocumentFormField field, string path)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = Label(field), Style = StyleResource("CaptionTextBlockStyle") });
        var rows = GetArray(path);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowPanel = new StackPanel { Spacing = 8 };
            rowPanel.Children.Add(new TextBlock
            {
                Text = $"{field.Label} {i + 1}",
                Style = StyleResource("BodyStrongTextBlockStyle"),
            });

            if (field.Fields.Count == 1 && field.Fields[0].Path == "$")
            {
                rowPanel.Children.Add(BuildTextField(field.Fields[0], $"{path}[{i}]", field.Fields[0].Kind == FormFieldKind.MultilineText));
            }
            else if (field.Fields.Count == 0)
            {
                rowPanel.Children.Add(new TextBlock
                {
                    Text = "Edit this structured item in the JSON view.",
                    Foreground = BrushResource("BrandMutedBrush"),
                    Style = StyleResource("CaptionTextBlockStyle"),
                });
            }
            else
            {
                foreach (var child in field.Fields)
                {
                    rowPanel.Children.Add(BuildField(child, JsonPath.Combine($"{path}[{i}]", child.Path)));
                }
            }

            var remove = new Button { Content = "Remove", Style = StyleResource("BrandSecondaryButtonStyle") };
            AutomationProperties.SetAutomationId(remove, $"Remove_{field.Id}_{i}_{SanitizeAutomationPath(path)}");
            var index = i;
            remove.Click += (_, _) =>
            {
                var array = GetOrCreateArray(path);
                if (index >= 0 && index < array.Count)
                {
                    array.RemoveAt(index);
                    UpdatePayloadText();
                    BuildFormFromDraft();
                }
            };
            rowPanel.Children.Add(remove);

            panel.Children.Add(new Border
            {
                BorderBrush = BrushResource("BrandHairlineBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Child = rowPanel,
            });
        }

        // A repeater with no child fields can only be edited in the JSON view; an "Add"
        // there would insert an empty, un-editable item that then fails validation.
        if (field.Fields.Count > 0)
        {
            var add = new Button { Content = $"Add {field.Label.ToLowerInvariant()}", Style = StyleResource("BrandSecondaryButtonStyle") };
            AutomationProperties.SetAutomationId(add, $"Add_{field.Id}_{SanitizeAutomationPath(path)}");
            add.Click += (_, _) =>
            {
                AddRepeaterItem(path, field);
                BuildFormFromDraft();
            };
            panel.Children.Add(add);
        }

        return panel;
    }

    private void AddRepeaterItem(string path, DocumentFormField field)
    {
        var array = GetOrCreateArray(path);
        if (field.Fields.Count == 1 && field.Fields[0].Path == "$")
        {
            array.Add("");
        }
        else
        {
            var item = new JsonObject();
            foreach (var child in field.Fields)
            {
                JsonPath.Set(item, child.Path, DefaultValue(child.Kind));
            }

            array.Add(item);
        }

        UpdatePayloadText();
    }

    private static object? DefaultValue(FormFieldKind kind) => kind switch
    {
        FormFieldKind.Checkbox => false,
        FormFieldKind.Number or FormFieldKind.Money => 0m,
        FormFieldKind.Repeater => new JsonArray(),
        _ => "",
    };

    private void ApplySignatureDefaults(string path, string? signatureKey)
    {
        if (string.IsNullOrWhiteSpace(signatureKey) || !path.EndsWith(".signatureImage", StringComparison.Ordinal))
        {
            return;
        }

        var prefix = path[..^".signatureImage".Length];
        var (name, qualifications) = signatureKey switch
        {
            "andy_patterson" => ("A. Patterson", "M.Inst.AEA"),
            "ed_mawdsley" => ("E. Mawdsley", "M.Inst.AEA"),
            "neil_oreilly" => ("N. D. O'Reilly", "M.Inst.AEA"),
            _ => ("", ""),
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            SetValue($"{prefix}.name", name);
            SetValue($"{prefix}.qualifications", qualifications);
        }
    }

    private JsonNode ParseDraft(string json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            return JsonNode.Parse(json) ?? new JsonObject();
        }

        return new JsonObject();
    }

    private string GetString(string path)
    {
        var node = GetNode(path);
        if (node is null)
        {
            return "";
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return node.ToJsonString().Trim('"');
    }

    private bool GetBool(string path)
    {
        var node = GetNode(path);
        if (node is JsonValue value && value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        return bool.TryParse(GetString(path), out var parsed) && parsed;
    }

    private JsonArray GetArray(string path) =>
        GetNode(path) as JsonArray ?? new JsonArray();

    private JsonArray GetOrCreateArray(string path)
    {
        var existing = GetNode(path) as JsonArray;
        if (existing is not null)
        {
            return existing;
        }

        var array = new JsonArray();
        SetValue(path, array);
        return array;
    }

    private JsonNode? GetNode(string path) => JsonPath.Navigate(_draftRoot, path);

    private void SetValue(string path, object? value)
    {
        _draftRoot ??= new JsonObject();
        JsonPath.Set(_draftRoot, path, value);
        _fieldErrors.Remove(path);
        UpdatePayloadText();
    }

    private void UpdatePayloadText()
    {
        if (_draftRoot is null)
        {
            return;
        }

        ViewModel.PayloadJson = _draftRoot.ToJsonString(CrJson.Relaxed);
    }

    private static string Label(DocumentFormField field) =>
        field.Required ? $"{field.Label} *" : field.Label;

    private static string ShortPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        try
        {
            return Path.GetFileName(value);
        }
        catch
        {
            return value;
        }
    }

    private static void UpdateUploadDisplay(TextBlock label, Image? preview, string value)
    {
        label.Text = ShortPath(value);
        if (preview is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            preview.Source = new BitmapImage(new Uri(value)) { DecodePixelHeight = 192 };
            preview.Visibility = Visibility.Visible;
            return;
        }

        preview.Source = null;
        preview.Visibility = Visibility.Collapsed;
    }

    private static Style? StyleResource(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) ? value as Style : null;

    private static Brush? BrushResource(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) ? value as Brush : null;

    private static string SanitizeAutomationPath(string path)
    {
        var chars = path.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }

    private sealed record BatchGuiManifest
    {
        public List<BatchGuiItem> Items { get; init; } = new();
    }

    private sealed record BatchGuiItem
    {
        public string TemplateId { get; init; } = "";
        public JsonElement Data { get; init; }
        public string? DataPath { get; init; }
        public string? Density { get; init; }
        public string? Out { get; init; }
    }

    // --------------------------------------------------------------- render

    private async void OnRenderClick(object sender, RoutedEventArgs e)
    {
        // Guard against overlapping renders: a double-click, the Ctrl accelerator firing
        // twice, or the post-install retry must not start a second concurrent pipeline.
        if (ViewModel.IsBusy)
        {
            return;
        }

        if (ViewModel.SelectedTemplate is null)
        {
            await ShowMessageAsync("No document type", "Choose a document type first.");
            return;
        }

        var scan = PlaceholderScanner.Scan(ViewModel.PayloadJson ?? string.Empty);
        if (scan.Any && !await ConfirmPlaceholdersAsync(scan))
        {
            return;
        }

        await RenderOnceAsync(allowInstall: true);
    }

    private async Task RenderOnceAsync(bool allowInstall)
    {
        ValidationBar.IsOpen = false;
        BeginBusy("Rendering document…");

        try
        {
            var outcome = await ViewModel.RenderAsync();

            switch (outcome.Kind)
            {
                case RenderOutcomeKind.Success:
                    await ShowResultAsync(outcome);
                    break;

                case RenderOutcomeKind.ValidationFailed:
                    ShowValidationErrors(outcome.Errors);
                    break;

                case RenderOutcomeKind.BrowserMissing when allowInstall:
                    await HandleBrowserMissingAsync(outcome.Message);
                    break;

                default:
                    ViewModel.StatusText = "Render failed.";
                    await ShowMessageAsync("Render failed", outcome.Message ?? "An unexpected error occurred.");
                    break;
            }
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ShowResultAsync(RenderOutcome outcome)
    {
        var result = outcome.Result!;

        ViewModel.HasRendered = true;
        ViewModel.StatusText = $"Rendered {result.SuggestedFileName} — SHA-256 {Shorten(result.Sha256)}.";

        PagesBadge.Text = result.PageCount == 1 ? "1 page" : $"{result.PageCount} pages";
        DensityBadge.Text = $"Density: {result.Density}";
        ResultBadges.Visibility = Visibility.Visible;

        if (result.Warnings.Count > 0)
        {
            ValidationBar.Severity = InfoBarSeverity.Warning;
            ValidationBar.Title = result.Warnings.Count == 1 ? "1 warning" : $"{result.Warnings.Count} warnings";
            ValidationBar.Message = string.Join("\n", result.Warnings);
            ValidationBar.IsOpen = true;
        }

        await ShowPdfAsync(result.Pdf);
        PdfTab.IsSelected = true;
        CheckpointDraft();
    }

    private void ShowValidationErrors(IReadOnlyList<string> errors)
    {
        _fieldErrors = MapFieldErrors(errors);
        BuildFormFromDraft();

        ViewModel.StatusText = errors.Count == 1
            ? "1 validation error — fix it and render again."
            : $"{errors.Count} validation errors — fix them and render again.";

        ValidationBar.Severity = InfoBarSeverity.Error;
        ValidationBar.Title = errors.Count == 1 ? "Payload validation failed" : $"Payload validation failed ({errors.Count} issues)";
        ValidationBar.Message = string.Join("\n", errors.Select(err => "• " + err));
        ValidationBar.IsOpen = true;
        ShowEditView();
    }

    private static Dictionary<string, List<string>> MapFieldErrors(IReadOnlyList<string> errors)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var error in errors)
        {
            var path = ExtractErrorPath(error);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!result.TryGetValue(path, out var list))
            {
                list = new List<string>();
                result[path] = list;
            }

            list.Add(error);
        }

        return result;
    }

    private static string? ExtractErrorPath(string error)
    {
        var markers = new[]
        {
            " is required.", " file was not found:", " must reference ", " must be a ", " has unknown type ",
        };
        foreach (var marker in markers)
        {
            var index = error.IndexOf(marker, StringComparison.Ordinal);
            if (index > 0)
            {
                return error[..index];
            }
        }

        return null;
    }

    // --------------------------------------------------------------- PDF preview

    private async Task ShowPdfAsync(byte[] pdf)
    {
        // WebView2 needs a URL; write the bytes to a fresh temp file per render and navigate
        // to it. A new file avoids a sharing lock when WebView2 still holds the previous PDF.
        var path = Path.Combine(
            Path.GetTempPath(),
            $"collisionrenderer_preview_{Guid.NewGuid():N}.pdf");

        await File.WriteAllBytesAsync(path, pdf);

        await PdfPreview.EnsureCoreWebView2Async();
        PdfPreview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        PdfPreview.Source = new Uri(path);

        var previous = _previewTempPath;
        _previewTempPath = path;
        TryDeleteTemp(previous);

        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        PdfPreview.Visibility = Visibility.Visible;
    }

    // --------------------------------------------------------------- save / open

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var result = ViewModel.LastResult;
        if (result is null)
        {
            return;
        }

        try
        {
            var picker = new FileSavePicker(App.WindowId)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(result.SuggestedFileName),
            };
            picker.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            await File.WriteAllBytesAsync(file.Path, result.Pdf);
            ViewModel.StatusText = $"Saved {Path.GetFileName(file.Path)}.";
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Could not save PDF", ex.Message);
        }
    }

    private async void OnOpenPdfClick(object sender, RoutedEventArgs e)
    {
        var result = ViewModel.LastResult;
        if (result is null)
        {
            return;
        }

        try
        {
            // Write to a unique folder so re-opening the same document doesn't collide with a
            // viewer that still holds the previous file open, while keeping a friendly name.
            var dir = Path.Combine(Path.GetTempPath(), $"cr_open_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, result.SuggestedFileName);
            await File.WriteAllBytesAsync(path, result.Pdf);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Could not open PDF", ex.Message);
        }
    }

    private async Task<string> RenderBatchAsync(string manifestPath, string outputFolder)
    {
        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<BatchGuiManifest>(manifestJson, CrJson.Options)
            ?? throw new RenderValidationException(new[] { "Batch manifest deserialised to null." });

        if (manifest.Items.Count == 0)
        {
            throw new RenderValidationException(new[] { "Batch manifest must contain at least one item." });
        }

        Directory.CreateDirectory(outputFolder);
        var failures = new List<string>();
        var rendered = 0;

        await using var renderer = CollisionRendererFactory.CreateRenderer();
        for (var i = 0; i < manifest.Items.Count; i++)
        {
            var item = manifest.Items[i];
            try
            {
                var json = ResolveBatchItemJson(item, manifestPath);
                var result = await renderer.RenderAsync(new RenderRequest
                {
                    TemplateId = item.TemplateId,
                    Json = json,
                    Options = ParseDensity(item.Density ?? "auto"),
                });

                var outPath = ResolveBatchOutputPath(outputFolder, item.Out, result.SuggestedFileName);
                var fullDir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                if (!string.IsNullOrEmpty(fullDir))
                {
                    Directory.CreateDirectory(fullDir);
                }

                await File.WriteAllBytesAsync(outPath, result.Pdf);
                rendered++;
            }
            catch (Exception ex) when (ex is RenderValidationException or KeyNotFoundException or FileNotFoundException or JsonException)
            {
                failures.Add($"Item {i + 1} ({item.TemplateId}): {ex.Message}");
            }
        }

        if (failures.Count == 0)
        {
            return $"Batch complete: {rendered} rendered to {outputFolder}.";
        }

        return $"Batch complete: {rendered} rendered, {failures.Count} failed.\n\n{string.Join("\n", failures)}";
    }

    private static string ResolveBatchItemJson(BatchGuiItem item, string manifestPath)
    {
        if (!string.IsNullOrWhiteSpace(item.DataPath))
        {
            var baseDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? Directory.GetCurrentDirectory();
            var path = Path.IsPathRooted(item.DataPath)
                ? item.DataPath
                : Path.Combine(baseDir, item.DataPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Batch data file not found: {path}");
            }

            return File.ReadAllText(path);
        }

        if (item.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new RenderValidationException(new[] { "Batch item requires either data or dataPath." });
        }

        return item.Data.GetRawText();
    }

    private static string ResolveBatchOutputPath(string outDir, string? itemOut, string suggestedFileName)
    {
        if (string.IsNullOrWhiteSpace(itemOut))
        {
            return Path.Combine(outDir, suggestedFileName);
        }

        return Path.IsPathRooted(itemOut)
            ? itemOut
            : Path.Combine(outDir, itemOut);
    }

    private static RenderOptions ParseDensity(string value) => value.ToLowerInvariant() switch
    {
        "auto" => new RenderOptions { Fit = DensityFit.Auto, Density = Density.Normal },
        "normal" => new RenderOptions { Fit = DensityFit.Fixed, Density = Density.Normal },
        "compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = Density.Compact },
        "ultra" or "ultra-compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = Density.UltraCompact },
        _ => throw new RenderValidationException(new[] { $"Unknown density '{value}'. Use auto|normal|compact|ultra." }),
    };

    // --------------------------------------------------------------- Chromium setup

    private async Task HandleBrowserMissingAsync(string? message)
    {
        ViewModel.StatusText = "The rendering engine (Chromium) is not installed yet.";

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "One-time setup required",
            Content = "Collision Renderer needs the Chromium rendering engine to produce PDFs. "
                      + "It only needs to be installed once. Install it now?",
            PrimaryButtonText = "Install now",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Primary,
        };

        var choice = await ShowDialogAsync(dialog);
        if (choice != ContentDialogResult.Primary)
        {
            return;
        }

        BeginBusy("Installing the Chromium engine… this can take a minute.");
        try
        {
            var exit = await Task.Run(() =>
                Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }));

            if (exit == 0)
            {
                ViewModel.StatusText = "Chromium installed. Rendering…";
                EndBusy();
                await RenderOnceAsync(allowInstall: false);
                return;
            }

            await ShowMessageAsync(
                "Installation failed",
                $"The Chromium installer exited with code {exit}. {message}");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Installation failed", ex.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    // --------------------------------------------------------------- keyboard

    private void OnRenderAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OnRenderClick(this, new RoutedEventArgs());
    }

    private void OnSaveDraftAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OnSaveDraftClick(this, new RoutedEventArgs());
    }

    private void OnBackAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = TryGoBackAsync();
    }

    // --------------------------------------------------------------- helpers

    private void CheckpointDraft()
    {
        if (ViewModel.SelectedTemplate is null)
        {
            return;
        }

        _dirty = false;
        _state.AddRecent(
            _authoringId,
            ViewModel.SelectedTemplate.Name,
            ViewModel.PayloadJson ?? string.Empty,
            DateTime.UtcNow.ToString("o"));
        _state.ClearAutosave(_authoringId);
    }

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        // WinUI permits only one ContentDialog open at a time; a second ShowAsync throws.
        // Serialise through this guard so overlapping async-void handlers can't crash the app.
        if (_dialogOpen)
        {
            return ContentDialogResult.None;
        }

        _dialogOpen = true;
        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // A dialog that can't be shown (already-open, or XamlRoot torn down mid-close)
            // must not bring down an async-void handler; treat it as dismissed.
            return ContentDialogResult.None;
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async Task<bool> ConfirmDiscardAsync(string title)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = "You have unsaved changes to this document. They will be lost.",
            PrimaryButtonText = "Discard",
            CloseButtonText = "Keep editing",
            DefaultButton = ContentDialogButton.Close,
        };

        return await ShowDialogAsync(dialog) == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmPlaceholdersAsync(PlaceholderScan scan)
    {
        var sample = scan.Samples.Count > 0 ? $" like {string.Join(", ", scan.Samples.Take(3))}" : string.Empty;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Placeholders still present",
            Content = $"This document still contains {scan.Count} unfilled placeholder prompt"
                      + (scan.Count == 1 ? "" : "s") + sample
                      + ". Render the PDF anyway?",
            PrimaryButtonText = "Render anyway",
            CloseButtonText = "Keep editing",
            DefaultButton = ContentDialogButton.Close,
        };

        return await ShowDialogAsync(dialog) == ContentDialogResult.Primary;
    }

    private static void TryDeleteTemp(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private void BeginBusy(string message)
    {
        ViewModel.BusyMessage = message;
        ViewModel.IsBusy = true;
        BusyOverlay.Visibility = Visibility.Visible;
    }

    private void EndBusy()
    {
        ViewModel.IsBusy = false;
        BusyOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowEditView()
    {
        FormTab.IsSelected = true;
    }

    private Task ShowMessageAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };
        return ShowDialogAsync(dialog);
    }

    private static string Shorten(string sha) =>
        sha.Length <= 12 ? sha : sha[..12];
}
