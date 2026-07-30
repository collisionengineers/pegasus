namespace Pegasus.EmailEvaluation.Desktop;

public sealed class MainForm : Form
{
    private readonly EmailEvaluationWorkflow workflow;
    private readonly Label statusLabel = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly Label errorLabel = new() { AutoSize = true, ForeColor = Color.Firebrick, Dock = DockStyle.Fill };
    private readonly Label suggestionLabel = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly TextBox fromText = ReadOnlyField();
    private readonly TextBox toText = ReadOnlyField();
    private readonly TextBox ccText = ReadOnlyField();
    private readonly TextBox dateText = ReadOnlyField();
    private readonly TextBox subjectText = ReadOnlyField();
    private readonly TextBox attachmentsText = ReadOnlyField();
    private readonly TextBox bodyText = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, BackColor = SystemColors.Window };
    private readonly ComboBox categoryCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox otherCategoryText = new() { Dock = DockStyle.Fill, Enabled = false };
    private readonly TextBox reasoningText = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Button fileButton = new() { Text = "File email", AutoSize = true, Enabled = false };
    private readonly Button skipButton = new() { Text = "Skip", AutoSize = true, Enabled = false };
    private readonly Button selectFolderButton = new() { Text = "Select folder", AutoSize = true };

    public MainForm(EmailEvaluationWorkflow workflow)
    {
        this.workflow = workflow;
        Text = "Pegasus local email evaluation";
        MinimumSize = new Size(900, 700);
        Width = 1100;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        categoryCombo.SelectedIndexChanged += (_, _) =>
        {
            var isOther = categoryCombo.SelectedItem is string value && value == "Other";
            otherCategoryText.Enabled = isOther;
            if (!isOther)
            {
                otherCategoryText.Clear();
            }
        };
        selectFolderButton.Click += SelectFolderAsync;
        skipButton.Click += SkipAsync;
        fileButton.Click += FileEmailAsync;
        Render(workflow.Snapshot);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 7 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        root.Controls.Add(selectFolderButton, 0, 0);
        root.Controls.Add(statusLabel, 1, 0);

        var headers = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        headers.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        headers.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddHeader(headers, "From", fromText, 0);
        AddHeader(headers, "To", toText, 1);
        AddHeader(headers, "Cc", ccText, 2);
        AddHeader(headers, "Date", dateText, 3);
        AddHeader(headers, "Subject", subjectText, 4);
        AddHeader(headers, "Attachments", attachmentsText, 5);
        root.Controls.Add(headers, 0, 1);
        root.SetColumnSpan(headers, 2);

        root.Controls.Add(bodyText, 0, 2);
        root.SetColumnSpan(bodyText, 2);

        root.Controls.Add(suggestionLabel, 0, 3);
        root.SetColumnSpan(suggestionLabel, 2);

        var categoryPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        categoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        categoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        categoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        categoryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        categoryPanel.Controls.Add(new Label { Text = "Category", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        categoryPanel.Controls.Add(categoryCombo, 1, 0);
        categoryPanel.Controls.Add(new Label { Text = "New category name", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        categoryPanel.Controls.Add(otherCategoryText, 3, 0);
        root.Controls.Add(categoryPanel, 0, 4);
        root.SetColumnSpan(categoryPanel, 2);

        root.Controls.Add(new Label { Text = "Why is this the correct category?", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        root.Controls.Add(reasoningText, 1, 5);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        actions.Controls.Add(fileButton);
        actions.Controls.Add(skipButton);
        actions.Controls.Add(errorLabel);
        root.Controls.Add(actions, 0, 6);
        root.SetColumnSpan(actions, 2);

        Controls.Add(root);
    }

    private static void AddHeader(TableLayoutPanel panel, string label, Control field, int row)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        panel.Controls.Add(field, 1, row);
    }

    private async void SelectFolderAsync(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose a folder containing .eml files." };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            Render(await workflow.SelectFolderAsync(dialog.SelectedPath));
        }
    }

    private async void SkipAsync(object? sender, EventArgs e) =>
        Render(await workflow.SkipAsync());

    private async void FileEmailAsync(object? sender, EventArgs e)
    {
        var selected = categoryCombo.SelectedItem as string ?? string.Empty;
        var family = selected == "Other" ? "Other" : selected.Split(" / ", 2)[0];
        var category = selected == "Other" ? "Other" : selected.Split(" / ", 2)[1];
        var result = await workflow.TryFileAsync(family, category, otherCategoryText.Text, reasoningText.Text);
        Render(result.Snapshot);
        if (result.Success)
        {
            reasoningText.Clear();
            categoryCombo.SelectedIndex = -1;
        }
    }

    private void Render(EvaluationSnapshot snapshot)
    {
        statusLabel.Text = snapshot.Status;
        errorLabel.Text = snapshot.Error ?? string.Empty;
        suggestionLabel.Text = snapshot.Suggestion;
        fileButton.Enabled = snapshot.CanFile;
        skipButton.Enabled = snapshot.CanSkip;
        if (snapshot.Message is null)
        {
            fromText.Clear();
            toText.Clear();
            ccText.Clear();
            dateText.Clear();
            subjectText.Clear();
            attachmentsText.Clear();
            bodyText.Clear();
        }
        else
        {
            fromText.Text = snapshot.Message.From;
            toText.Text = snapshot.Message.To;
            ccText.Text = snapshot.Message.Cc;
            dateText.Text = snapshot.Message.SentAt;
            subjectText.Text = snapshot.Message.Subject;
            attachmentsText.Text = string.Join(", ", snapshot.Message.AttachmentNames);
            bodyText.Text = snapshot.Message.Body;
        }

        if (categoryCombo.Items.Count == 0)
        {
            foreach (var category in snapshot.Categories)
            {
                categoryCombo.Items.Add(category.DisplayName);
            }

            categoryCombo.Items.Add("Other");
        }
    }

    private static TextBox ReadOnlyField() => new() { ReadOnly = true, Dock = DockStyle.Fill, BackColor = SystemColors.Window };
}
