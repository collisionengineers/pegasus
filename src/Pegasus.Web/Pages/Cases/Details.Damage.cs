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
/// The Damage section's members: the incident narrative. Moved out of
/// Details.cshtml.cs unchanged so the section has its own file
/// (#1730, R1112).
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>
    /// The Damage section's Incident narrative: the report's Nature of Incident
    /// block as Core composes it for this view's work (operator, 24 September
    /// 2026), in every state the workspace loads. Null while there is none.
    /// </summary>
    public string? IncidentNarrative { get; private set; }

    /// <summary>
    /// Whether the Incident narrative is the sentences the impacts compose, so
    /// the Damage section recomposes it while its impacts are edited; false
    /// when the Engineer has written their own Nature of Incident wording.
    /// </summary>
    public bool IncidentNarrativeFollowsImpacts { get; private set; } = true;
}
