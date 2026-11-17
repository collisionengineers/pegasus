using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.AiWork;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Presentation;
using Pegasus.Infrastructure.Email;
using EstimateVatLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateVat;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;
using FrameLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame;
using Labels = Pegasus.Web.Presentation.OperatorLabels;
using EditorLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case details section's members: the claim source choices and the
/// sign-off Engineer the section offers. Moved out of Details.cshtml.cs
/// unchanged so the section has its own file (#1730, R1112).
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>
    /// The active Claim source records the Overview's claim source select
    /// offers while the record is being edited; each option carries the
    /// record's notes and contact line so the notes band follows a change
    /// without another request.
    /// </summary>
    public IReadOnlyList<ContactChoice> ClaimSourceChoices { get; private set; } = [];

    public Guid? SelectedSignOffEngineerId { get; private set; }
}
