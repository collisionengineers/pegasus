using Pegasus.Core.Assessment;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The parameter descriptions many Automation tools share, word for word, so
/// one sentence is written once.
/// </summary>
internal static class AutomationToolText
{
    public const string CaseId = "The durable Pegasus case identifier.";

    public const string ExpectedCaseVersion = "The case version the caller observed; a stale value fails closed.";

    public const string OperationKey = "Caller idempotency key prefixed 'mcp:'.";

    public const string EditLeaseToken = "Edit lease token from pegasus_edit_begin for multi-step work, presented on every write until pegasus_edit_end; omit it and the tool holds the lease for this one command.";

    public const string Cursor = "Opaque cursor returned by the previous page; omit for the first page.";

    public const string PageSize = "Page size between 1 and 100; omit for 50.";

    // The Automation's own Glass's sessions (design 07 section 9 question 1,
    // option a): each tool's description and the sentences a refused call
    // answers with, in one place.

    // Operator, 18 November 2026: R1178; its last sentence, on read, is a
    // draft of 19 November 2026 (R1206) for the operator to approve.
    public const string GlassSessionGet =
        "The Automation's own Glass's session on a Case: its state and version, whether the Glass's account is free or which Case holds it, each position it added or removed with Glass's own figures, and the Draft repair spec a finished session landed as. With read, Glass's is asked once and nothing changes there: positions answers the positions the estimate holds and Glass's running totals, position answers one position's figures, and calculation answers Glass's calculation.";

    // Operator, 18 November 2026: R1178
    public const string GlassSessionAct =
        "Start, finish or close the Automation's own Glass's session on a Case. start makes a new Glass's estimate for the Case's vehicle and opens it, or continues the Automation's session already open on that Case. finish saves the estimate in Glass's and brings it back as a Draft repair spec; only staff put a repair spec in use. close ends the session without saving, with a reason. The Glass's account holds one session at a time: while it is in use on another Case, start is refused and names that Case. start and finish take the Case version and hold its edit lease for this one command; finish and close take the session version. A call waits up to 45 seconds; read a session that is still working with pegasus_glass_session_get.";

    // Operator, 18 November 2026: R1178
    public static string GlassAccountHeldByStaff(string caseReference) =>
        $"The Glass's account is in use by a member of staff on Case {caseReference}.";

    // Operator, 18 November 2026: R1178
    public static string GlassAccountHeldByAutomation(string caseReference) =>
        $"The Glass's account is in use by the Automation's session on Case {caseReference}; finish or close it first.";

    // Operator, 18 November 2026: R1178
    public const string GlassNoSession = "The Automation has no Glass's session open on this Case.";

    // Operator, 18 November 2026: R1178
    public const string GlassEstimateEmpty = "The estimate has no positions; close the session instead.";

    // Operator, 18 November 2026: R1178. The equipment sentence is a draft
    // for the operator under R1206 (19 November 2026).
    public const string GlassCatalogueSearch =
        "Search the open Glass's estimate's part list and its special operations by name, part code or part number. Each row carries the value pegasus_glass_position_act adds it by, its side, and whether its part can be painted. A part whose row asks Glass's equipment question carries the question; with row, the search answers that one row and each answer to its question that leads to a part. Glass's is asked at most twice; nothing changes.";

    // Operator, 18 November 2026: R1178. The equipment sentences, and the
    // preview and inputs sentences, are drafts for the operator under R1206
    // (19 November 2026).
    public const string GlassPositionAct =
        "Add a position to the Automation's open Glass's estimate, change or remove one already on it, or preview the dialog a position would be added or shown by. add takes a row pegasus_glass_catalogue_search returned and posts Glass's own dialog as Glass's serves it, with what inputs gives it as a person would: another operation the dialog offers, the Paint or Part and Paint button, Both sides, the optional parts to tick and the OK to a part with no figures. It also types hours, a price and overlap hours as a person would, with the Automation's own reason when the hours are above Glass's own; Pegasus never writes a reason. On a part that can be painted it also chooses every paint field and types the paint price, hours and reason. No part number or description is typed. A part is added by the row named, as a person's click on that row adds it. A part whose row asks Glass's equipment question, a wheel's included, is added with inputs.equipment, the answer a person would give, with any operation; Glass's keeps that answer for the vehicle. Without it, or with an answer Glass's would not take, the add is refused with the question and nothing is sent. A part the vehicle's equipment hides is refused with what hides it. remove takes a position id. change takes the id of any position on the estimate and inputs, and posts the position's own dialog, the one Glass's opens for it, with the hours, price, overlap hours, reason and paint inputs gives, the OK to no figures and the optional parts to tick; it reads the estimate before and after, and the position's own figures once changed. preview takes what add takes, or a position id with the inputs a change would give, and answers the dialog as a form: its operations with Glass's figures for each, its fields, its paint section, its buttons, and for the inputs given what add would send and each question on the way. preview posts nothing and records nothing; it takes no session version. add, change and remove take the session version and an operation key; the same key answers the recorded outcome and never sends twice. not_confirmed means Glass's did not confirm the change: read the estimate before adding the same position again, or the position before changing it again.";

    // The parameter and field descriptions and the refusals of the catalogue
    // search and the position act that the ruled set (R1178) does not hold.

    public const string GlassSearchTerm = "Matched against each row's description, part code or part number, ignoring case; 1 to 200 characters. Not needed with row.";

    public const string GlassSearchKind = "part, special_operation or any; omit for any. Not with list.";

    public const string GlassSearchSide = "L or R: only parts of that side, and no special operations.";

    public const string GlassSearchTermRefused = "term must be 1 to 200 characters.";

    public const string GlassSearchList = "The one list to search, as a person opens it: parts, special_operations (Free positions parts, the user defined parts; user_defined_parts is the same list), additional_labour, user_defined_paint, labour_descriptions, tyres, bulbs, special_metal, special_plastic or paint_areas (Areas > Paint). Omit to search parts and special_operations together. Glass's is asked once; tyres is a large list. Not with kind or row. A list the account's editor does not offer answers no rows and glass.list.not_offered.";

    public const string GlassSearchListRefused = "list must be one of: parts, special_operations, user_defined_parts, additional_labour, user_defined_paint, labour_descriptions, tyres, bulbs, special_metal, special_plastic, paint_areas, and is not given with kind or row.";

    public const string GlassCatalogueDialog = "The dialog the row opens: part, part_paintable or paint.";

    public const string GlassCatalogueGroup = "Glass's own name for the list the row is in.";

    public const string GlassSearchKindRefused = "kind must be one of: part, special_operation, any.";

    public const string GlassSideRefused = "side must be L or R.";

    public const string GlassCatalogueRow = "The value pegasus_glass_position_act adds the row by: a special operation's code, or a part's list row.";

    public const string GlassCatalogueKind = "part, special_operation (a row of an Additional list, added by part.php) or paint (a paint row, added by its paint dialog).";

    public const string GlassCatalogueSide = "L, R, both, or none for a part with no side.";

    public const string GlassCataloguePaintable = "Whether the part can be painted.";

    public const string GlassCatalogueCode = "The glass.* code when Glass's did not answer a list, or the row asked for is not a part on it; no rows are then answered.";

    public const string GlassExpectedSessionVersion = "add, change and remove: the session version from pegasus_glass_session_get or the last Glass's tool answer; a stale value fails closed. preview takes none.";

    public const string GlassPositionAction = "add, change, remove or preview.";

    public const string GlassAddKind = "add and preview: part, special_operation (a row of any Additional list but the special tables and user defined paint) or paint (a special table, user defined paint or paint area row), as the search answered it.";

    public const string GlassAddRow = "add and preview: the row value pegasus_glass_catalogue_search returned.";

    public const string GlassAddOperation = "add and preview, a part only: part, part_and_paint, paint, repair or remove_and_refit. part is the dialog's own operation pressed with Part; part_and_paint and paint press those buttons on a part that can be painted; repair chooses Glass's R and answers OK to its question when the dialog serves no figures; remove_and_refit chooses Glass's UI. With part, inputs.operation and inputs.button choose instead.";

    public const string GlassAddSide = "add and preview, a part only: L or R, checked against the row before anything is sent. To add both sides, use inputs.side.";

    public const string GlassRemovePositionId = "remove, change, or preview of a position on the estimate: a position id from the acts or the estimate.";

    public const string GlassPositionActionRefused = "The action must be one of: add, change, remove, preview.";

    public const string GlassAddKindRefused = "kind must be one of: part, special_operation, paint.";

    public const string GlassAddRowRefused = "row is required for add.";

    public const string GlassAddOperationRefused = "operation must be one of: part, part_and_paint, paint, repair, remove_and_refit.";

    public const string GlassAddOperationMissing = "operation is required for a part.";

    public const string GlassAddOperationNotForSpecial = "operation is given for a part only.";

    public const string GlassRemovePositionIdRefused = "positionId is required for remove.";

    public const string GlassSessionWorking = "The Automation's Glass's session is working; read it with pegasus_glass_session_get and act again once it is not.";

    public const string GlassActOutcome = "pending, added, changed, removed, not_confirmed or refused.";

    public const string GlassActCode = "The glass.* code of a refusal, or of a change Glass's did not confirm. " + GlassEquipmentCodes;

    // The equipment refusals' codes, one sentence each (R1206; 19 November
    // 2026). Drafts for the operator to approve on the merge request.
    public const string GlassEquipmentCodes =
        "glass.build.equipment: the row asks Glass's equipment question; answer it with inputs.equipment. "
        + "glass.build.equipment_hidden: the vehicle's equipment hides this part on its drawing, so Glass's does not show it; hiddenBy names that equipment and the rows whose question can change it.";

    public const string GlassActPositionIds = "The positions the add put on the estimate.";

    public const string GlassActDescription = "The description Glass's dialog served.";

    public const string GlassActPrice = "The price Glass's dialog served.";

    public const string GlassActHours = "The hours Glass's dialog served.";

    public const string GlassActMandatoryParts = "The parts Glass's listed on its Mandatory parts page. Those ticked were added: the ones Glass's ticked itself, and the optional ones inputs.optionalParts named.";

    public const string GlassActMandatoryPartTicked = "Whether the part was ticked when the page was confirmed, and so added.";

    public const string GlassActMandatoryPartOptional = "Whether the page let the part be left off; only these can be named in inputs.optionalParts.";

    public const string GlassActMandatoryPartId = "The value inputs.optionalParts names this one box by: the code, or the code and #n for the n-th listing of a code the page lists more than once.";

    public const string GlassActs = "Each position act the Automation asked for on this session, in order.";

    // A part's equipment question (R1206; design 10 section 3). Drafts for
    // the operator to approve on the merge request. Glass's own option names
    // and its own "Criteria affects other parts!" are passed through as
    // Glass's gives them.

    public const string GlassSearchRow = "A part's row from an earlier search, a head or a variant: the search answers that row's head alone, with each answer to its equipment question that leads to a part, and for a variant the answers that lead to it.";

    public const string GlassCatalogueEquipment = "Present only when Glass's asks an equipment question before it adds this part: the options as Glass's dialog shows them, and with row each answer that leads to a part.";

    public const string GlassEquipmentOptionCode = "The code inputs.equipment names to tick the option.";

    public const string GlassEquipmentOptionName = "Glass's own name for the option.";

    public const string GlassEquipmentOptionTicked = "Whether Glass's holds the option for the vehicle now, so its dialog shows it ticked.";

    public const string GlassEquipmentOptionGreyed = "Glass's dialog greys the option out; an answer may not name it.";

    public const string GlassEquipmentOptionGroup = "Options of one group exclude each other; an answer names at most one of them.";

    public const string GlassEquipmentOptionNote = "Glass's own note on an option another position uses.";

    public const string GlassEquipmentAnswers = "With row: each answer that leads to a part. An answer not listed is refused.";

    public const string GlassEquipmentAnswerTicked = "The option codes the answer ticks; an entry of mapParts or mapHours needs these.";

    public const string GlassEquipmentAnswerRows = "The rows Glass's dialog is asked for: the part's row and its Replace hours row. An entry of mapParts or mapHours names its one row.";

    public const string GlassEquipmentAnswerFigures = "As Glass's part list gives it; the add answers with the figures Glass's dialog serves.";

    public const string GlassEquipmentMapParts = "With row, when the answers are too many to list: the part rows Glass's equipment map holds, each with the options it needs.";

    public const string GlassEquipmentMapHours = "With row, when the answers are too many to list: the Replace hours rows Glass's equipment map holds, each with the options it needs.";

    public const string GlassAddInputs = "add, change and preview: what to give Glass's dialog for the position, as a person would; omit it with add to give nothing and take the dialog as served. A change gives at least one.";

    public const string GlassInputsEquipment = "A part whose row asks Glass's equipment question, with any operation: the option codes to tick, from the row's equipment in pegasus_glass_catalogue_search; [] ticks none. It is the whole answer: an option not named is unticked, even one the vehicle has now. Glass's keeps the answer for the vehicle, as it keeps a person's. Omit it for a row that asks nothing.";

    public const string GlassInputsEquipmentRefused = "inputs.equipment is given only to add a part, and holds option codes, none blank.";

    // The dialog's own inputs and the preview (R1206, 19 November 2026):
    // drafts for the operator to approve on the merge request. Glass's own
    // labels, option texts and question texts pass through as Glass's gives
    // them.

    public const string GlassInputsOperation = "With operation part: Glass's own code for another operation the part's dialog offers, as preview lists it, such as I, AE, AD, P or Z. The dialog then sends the price and hours its own script sets for that operation. Renew (AN) opens its old-for-new window, which Pegasus asks for as the dialog does and answers as inputs.renew says. Paintless dent repair (DD) reloads the dialog, as in the editor: Pegasus submits the form as its script does and posts the dialog Glass's answers, with the rest of inputs. An operation the script holds no figures for is sent with 0.00 once inputs.confirmNoFigures answers its question. Only an operation preview marks changeable is taken. Not with inputs.button paint.";

    public const string GlassInputsButton = "With operation part, on a part that can be painted: part, paint or part_and_paint, the button pressed. The paint section is sent as Glass's serves it, unless inputs.paint chooses it. With change, the button the position's dialog shows is pressed when none is given. A part that cannot be painted has no paint buttons and the add is refused with glass.row.not_paintable.";

    public const string GlassInputsSide = "both: ticks Both in the dialog of a part that offers it, with any button and operation, as a person ticks it there. Glass's then opens the other side's own dialog, which is pressed with the same button and given the same inputs.operation and inputs.confirmNoFigures, and otherwise posted as served. Typed figures, the dialog's fields and paint choices go on one side at a time: give them with no side, or change the other side afterwards. If the other dialog does not come or is not the one expected, the first side stays added and the act's code is glass.row.other_side_not_added.";

    public const string GlassInputsOptionalParts = "The optional parts to tick on Glass's Mandatory parts page when it comes after the post, one box each as a person ticks them: by its id from an earlier act's mandatoryParts, such as 1102450.ER.#2 for the second listing of a code the page lists twice, or by its code, such as 1017810.ER., which ticks the first optional listing of that code. A part the page does not list as optional is not ticked. Which parts the page lists cannot be known before the post.";

    public const string GlassInputsConfirmNoFigures = "true answers OK when the dialog asks before sending a part with no price and no hours. Repair is answered OK without it. Any other operation served with no figures is refused without it, and nothing is sent.";

    public const string GlassInputsOperationRefused = "inputs.operation is Glass's own code for an operation the dialog offers, and is given with operation part only, and not with inputs.button paint.";

    public const string GlassInputsButtonRefused = "inputs.button must be part, paint or part_and_paint, and is given with operation part or with change only.";

    public const string GlassInputsSideRefused = "inputs.side must be both, and is not given with typed figures, inputs.place or inputs.paint.";

    public const string GlassInputsOptionalPartsRefused = "inputs.optionalParts holds part codes, none blank or repeated, and is given for a part or with change only.";

    public const string GlassInputsConfirmNoFiguresRefused = "A special operation takes only inputs.hours, inputs.price, inputs.overlapHours and inputs.reason, and a remove takes no inputs.";

    // Typed figures and the reason (R1206, 19 November 2026): drafts for the
    // operator to approve on the merge request.

    public const string GlassInputsHours = "The labour hours to type in place of Glass's own, for a special operation or a part. Sent with two decimal places after a full stop, such as 2.80, as a person types them; from 0 to 1000. Above Glass's own hours, which preview gives as reasonAbove, the dialog asks for a reason: give inputs.reason, or the add is refused with glass.build.reason_required and nothing is posted.";

    public const string GlassInputsPrice = "The price to type in place of Glass's own, where inputs.hours may be given. Sent with two decimal places after a full stop, such as 1234.00; from 0 to 999999.99.";

    public const string GlassInputsOverlapHours = "The overlap hours to type, where inputs.hours may be given. Sent as inputs.hours is; from 0 to 1000. More overlap hours than the hours sent is refused with glass.build.overlap_above_hours, because Pegasus could not read that position back when the estimate returns.";

    public const string GlassInputsReason = "The Automation's own reason for hours above Glass's own, 1 to 150 characters, given with inputs.hours. Glass's keeps it on the position. It is sent only when the dialog asks for one; given when it does not, the add is refused with glass.build.reason_not_asked.";

    public const string GlassInputsHoursRefused = "inputs.hours is a number from 0 to 1000 with at most two decimal places, and is not given with inputs.side both.";

    public const string GlassInputsPriceRefused = "inputs.price is a number from 0 to 999999.99 with at most two decimal places, given where inputs.hours may be.";

    public const string GlassInputsOverlapHoursRefused = "inputs.overlapHours is a number from 0 to 1000 with at most two decimal places, given where inputs.hours may be.";

    public const string GlassInputsReasonRefused = "inputs.reason is 1 to 150 characters and is given with inputs.hours.";

    public const string GlassPreviewFieldInput = "The member of inputs that changes this field, such as inputs.hours; absent for a field Pegasus does not change.";

    public const string GlassPreviewFieldReasonAbove = "On the hours field: Glass's own hours for the served operation. Typed hours above them need inputs.reason; absent where Glass's asks no reason.";

    public const string GlassActServed = "The price, hours and overlap hours Glass's dialog served for the operation sent, and its paint, before anything was typed or chosen.";

    public const string GlassActSent = "The price, hours and overlap hours the post sent, and its paint, typed and chosen ones included, recorded just before the post.";

    // Paint choices (R1206, 19 November 2026): drafts for the operator to
    // approve on the merge request.

    public const string GlassInputsPaint = "On a part that can be painted, pressed with Paint or Part and Paint (or with change): every paint choice a person makes. Pegasus makes them in this order, each with the dialog's own script: the area, the paint type, the procedure, the method, the level, Surcharge, Coloured clear, scratch-resistant clear, then the typed paint price, hours and reason.";

    public const string GlassInputsPaintCoats = "The paint type by Glass's own value, as preview's paint choices give it, such as 2 (2 Coat) or 3 (3 Coat). Choosing it sets the level, price and hours as Glass's script does.";

    public const string GlassInputsPaintLevel = "The paint level by Glass's own value, one the paint type offers in preview's paint choices, such as 1 to 4, B or S on metal and R, N, G, 2 or 3 on plastic. Choosing it sets the paint price and hours as Glass's script does.";

    public const string GlassInputsPaintSurcharge = "true ticks Surcharge and false unticks it. Where the paint type fixes Surcharge, a different value is refused.";

    public const string GlassInputsPaintHours = "The paint labour hours to type in place of Glass's own, sent as inputs.hours is; from 0 to 1000. Glass's asks no reason for them.";

    // The dialog's own fields (R1206, 19 November 2026): drafts for the
    // operator to approve on the merge request.

    public const string GlassInputsPlace = "The side boxes to tick, as a person ticks them, where the dialog lets its side be changed: one of the sides preview's Place field offers, L, R, V (front), H (rear), VL, VR, HL or HR. The dialog then reads its figures and part number for that side.";

    public const string GlassInputsTrade = "The labour type by Glass's own value, one preview's Craft field offers, such as C (Body), M (Mechanical) or E (Electrical).";

    public const string GlassInputsPartNumber = "The part number to type in the dialog's part number box, 1 to 20 characters.";

    public const string GlassInputsLabourDescription = "The labour description to type in the dialog's labour description box, as a person types or picks it, 1 to 150 characters.";

    public const string GlassInputsDescription = "A special operation's own description, typed in place of the row's, 3 to 250 characters.";

    public const string GlassInputsOutsideWork = "true ticks outside work where the dialog has it (a special operation's): Glass's then chooses ZK and greys the hours, the overlap hours and the labour type, so inputs.hours and inputs.overlapHours are refused with it.";

    public const string GlassInputsPanel = "true adds the part through the quick repair panel under the editor's drawing instead of its dialog, as a person does with one press: New for operation part, Remove and Install for remove_and_refit, Repair for repair with inputs.hours as the panel's repair hours (one decimal place, 0.0 when not given). With inputs.side both the panel adds each side in turn. On a part that can be painted the panel adds its paint too, at the paint level it chooses itself; inputs.paint.area chooses the paint area where the panel's paint box offers more than one. The panel sets nothing else: no price, typed figures, reason, labour type, part number, overlap hours, optional parts, paint type, Surcharge or clear coats, so use the dialog for those. A part already on the estimate, a button the panel greys, an area it does not offer and Both on a part with no other side are refused with glass.panel.refused before anything is sent. Add only; preview shows the dialog route.";

    public const string GlassInputsPanelRefused = "inputs.panel is true, with action add, kind part and operation part, remove_and_refit or repair; it takes only inputs.side, inputs.equipment, inputs.hours with repair (one decimal place) and inputs.paint.area. Nothing was sent to Glass's.";

    public const string GlassInputsRenew = "Renew's old-for-new window, answered as a person answers it: with inputs.operation AN only. The window shows the original price, hours and, on a part with its paint, the paint figures; each deduction given is typed and the window's OK sends the price and hours less it, with its reason in Glass's own words. A deduction not given stays as the window serves it (none). Deductions that leave a negative price or hours are refused with glass.build.deduction_below_zero, because Pegasus could not read that position back when the estimate returns. Not on a change.";

    public const string GlassInputsRenewPriceDeduction = "The deduction from the original price: a percentage, 0 to 100, unless priceAmount is true, then pounds. Two decimal places at most.";

    public const string GlassInputsRenewPriceAmount = "true makes priceDeduction an amount in pounds (the window's £) instead of a percentage.";

    public const string GlassInputsRenewHoursDeduction = "The deduction from the original labour hours: a percentage, 0 to 100, unless hoursAmount is true, then hours. Two decimal places at most.";

    public const string GlassInputsRenewHoursAmount = "true makes hoursDeduction an amount in hours (the window's Hour) instead of a percentage.";

    public const string GlassInputsRenewPaintDeduction = "The deduction from the original paint price and hours, a percentage, 0 to 100, on a part with its paint.";

    public const string GlassInputsRenewRefused = "inputs.renew is given only with inputs.operation AN, with at least one deduction, each 0 or more with two decimal places at most, a percentage at most 100. Nothing was sent to Glass's.";

    public const string GlassInputsOverlapFull = "true presses the overlap hours' Full button where the dialog has it, which copies the hours sent into the overlap hours.";

    public const string GlassInputsPlaceRefused = "inputs.place is one of L, R, V, H, VL, VR, HL or HR, and is not given with inputs.side both.";

    public const string GlassInputsTradeRefused = "inputs.trade is Glass's own value for a labour type, not blank.";

    public const string GlassInputsPartNumberRefused = "inputs.partNumber is 1 to 20 characters.";

    public const string GlassInputsLabourDescriptionRefused = "inputs.labourDescription is 1 to 150 characters.";

    public const string GlassInputsDescriptionRefused = "inputs.description is 3 to 250 characters and is given for a special operation, or with change, only.";

    public const string GlassInputsFieldRefused = "inputs.outsideWork and inputs.overlapFull are given to add, change or preview a position only.";

    public const string GlassInputsPaintArea = "The paint area by Glass's own value, as preview's paint section lists them. Choosing it sets the paint type, level, price and hours as Glass's script does.";

    public const string GlassInputsPaintProcedure = "The paint procedure by Glass's own value, one the paint type and level offer in preview's fields (Verf), such as 31. Choosing it sets the paint state, price and hours as Glass's script does.";

    public const string GlassInputsPaintMethod = "The paint method by Glass's own value, one preview's fields offer (Meth), such as 61. Choosing it sets the paint state, price and hours as Glass's script does.";

    public const string GlassInputsPaintColouredClear = "true ticks Coloured clear paint and false unticks it. Where the paint state fixes it, a different value is refused.";

    public const string GlassInputsPaintScratchResistant = "true ticks scratch-resistant clear where the paint area and type let it be ticked; elsewhere it is refused.";

    public const string GlassInputsPaintPrice = "The paint price to type in place of Glass's own, sent as inputs.price is; from 0 to 999999.99.";

    public const string GlassInputsPaintReason = "The Automation's own paint reason, 1 to 150 characters, as a person types it in Glass's paint reason window. Glass's asks for none; it is sent as given.";

    public const string GlassInputsPaintRefused = "inputs.paint gives area, coats, level, procedure, method, surcharge, colouredClear, scratchResistant, price, hours or reason, none blank, figures from 0 with at most two decimal places (hours to 1000, price to 999999.99), a reason of 1 to 150 characters, and is given with operation paint or part_and_paint, inputs.button paint or part_and_paint, or change; not with inputs.side.";

    public const string GlassActPaintFigures = "On a part that can be painted: the paint type, level, price, labour hours and Surcharge.";

    public const string GlassPreviewPaintChoices = "Each paint type and level the dialog offers in its paint area, with the price and hours Glass's script sets for them.";

    public const string GlassPreviewPaintPositionsOwn = "The dialog is of a position on the estimate: its paint figures are the position's own, and Glass's own are not shown.";

    public const string GlassPreviewPaintSurcharge = "Whether Surcharge is ticked as served.";

    public const string GlassPreviewPaintSurchargeFixed = "The served paint type fixes Surcharge, so it cannot be changed.";

    public const string GlassPreviewPaintChoiceFigures = "As Glass's script sets it for this paint type and level; absent where it has none.";

    public const string GlassPreviewPositionAlone = "A preview of a position on the estimate takes positionId, and inputs to preview a change; not kind, row, operation or side.";

    public const string GlassChangePositionAndInputs = "change takes positionId and inputs, and not kind, row, operation or side.";

    public const string GlassActPosition = "change: the position's figures as Glass's holds them once changed; absent when Glass's did not answer them.";

    public const string GlassActOfAddOrRemove = "The act, for add, change and remove; absent for preview.";

    public const string GlassPreviewDialog = "preview: the dialog as a form, when Glass's served it. Nothing was posted.";

    public const string GlassPreviewCode = "preview: the glass.* code when no dialog was read, such as a row that is not certain or an equipment question with no answer. " + GlassEquipmentCodes;

    // A part's variants and what hides a part (R1206; 19 November 2026). Drafts for the operator.

    public const string GlassCatalogueVariant = "Present when a variant row's own part number matched: the variant's row and part number. Search again with row set to the variant's row for the answers that lead to it.";

    public const string GlassCatalogueVariantAnswers = "With row set to a variant's row: each answer to the head's equipment question that leads to that variant; add the head's row with one of them.";

    public const string GlassHiddenBy = "A part the vehicle's equipment hides: each stored value that hides it, with Glass's own name.";

    public const string GlassHidingHas = "true: the vehicle has it, and it hides the part; false: the part needs it, and the vehicle lacks it.";

    public const string GlassHidingAnsweredOn = "The rows on the same drawing whose equipment question offers it; an answer there changes it.";

    public const string GlassPreviewEquipment = "preview, with glass.build.equipment: the part's equipment question, as the search gives it.";

    public const string GlassPreviewKind = "part_paintable (a part with a paint section and the Part, Paint and Part and Paint buttons), part, special_operation, or paint (a paint position's own dialog: its paint section, its sides and OK, changed with inputs.paint and inputs.place).";

    public const string GlassPreviewOperations = "Each operation the dialog's list offers, in Glass's order.";

    public const string GlassPreviewOperationCode = "Glass's own code: the value inputs.operation takes.";

    public const string GlassPreviewText = "Glass's own text, as the dialog shows it.";

    public const string GlassPreviewOperationFigures = "As the dialog serves it for this operation on the served side; absent when Glass's gives none.";

    public const string GlassPreviewGlassHours = "Glass's own hours for this operation, which the dialog compares typed hours against.";

    public const string GlassPreviewServed = "The dialog serves this one selected.";


    public const string GlassPreviewChangeable = "Whether Pegasus can change it yet. An operation that reloads the dialog or opens a window Pegasus does not answer cannot be chosen.";

    public const string GlassPreviewFields = "Each field a person sees, in the dialog's order. Each field marked changeable names the input that changes it.";

    public const string GlassPreviewFieldName = "The field's name in Glass's form.";

    public const string GlassPreviewFieldLabel = "Glass's own label beside the field, when the dialog has one.";

    public const string GlassPreviewFieldValue = "The value the dialog serves; for a tick box, true or false.";

    public const string GlassPreviewFieldDisabled = "The dialog serves the field greyed out.";

    public const string GlassPreviewPaint = "A part that can be painted: the paint area, paint type, paint level, price and labour time as Glass's serves them, and each paint type and level it offers.";

    public const string GlassPreviewButtons = "Each button the dialog offers, by its name and Glass's own text.";

    public const string GlassPreviewSends = "What add with the same inputs would send, and each question on the way.";

    public const string GlassPreviewSendsButton = "Glass's own text of the button add would press.";

    public const string GlassPreviewSendsCode = "The glass.* code add would be refused with before anything is posted; absent when add would post.";

    public const string GlassPreviewQuestions = "Each question the dialog or Glass's would ask on the way.";

    public const string GlassPreviewQuestionKind = "no_figures: the dialog asks before sending no price and no hours. reload: the operation reloads the dialog, which add follows. old_for_new: Renew opens its old-for-new window, which add asks for and answers as inputs.renew says. reason: more hours than Glass's own, which inputs.reason answers. other_side: Both opens the other side's dialog. mandatory_parts: Glass's may list mandatory parts after the post; which, if any, cannot be known before it.";

    public const string GlassPreviewQuestionText = "Glass's own words, where the dialog holds them.";

    public const string GlassPreviewQuestionAnswered = "Whether the inputs given answer it.";

    public const string GlassActInputs = "What the add or change gave Glass's.";

    public const string GlassActEquipment = "An add refused on a part's equipment question: the question, as the search gives it.";

    public const string GlassActRowPair = "The rows Glass's dialog was asked for, as a person's click on the row, with the equipment answer, chooses them.";

    public const string GlassActEquipmentChanged = "The options whose value Glass's held for the vehicle the answer changed, each with its new tick.";

    public const string GlassActEquipmentStored = "The answer was sent to Glass's, which keeps it for the vehicle even when the add was then refused.";

    // The read of the open estimate (R1206, 19 November 2026): drafts for
    // the operator to approve. Glass's own labels are passed through.

    public const string GlassRead = "Optional: positions, position or calculation. Glass's is asked once; nothing changes there.";

    public const string GlassReadPositionId = "read position: a position id from the estimate's positionIds.";

    public const string GlassReadRefused = "read must be one of: positions, position, calculation.";

    public const string GlassReadPositionIdRefused = "positionId is required for read position, as the estimate's positionIds list it.";

    public const string GlassReadPositionIdNotForRead = "positionId is given with read position only.";

    public const string GlassReadEstimate = "Present when read was positions and Glass's answered.";

    public const string GlassReadTotals = "Glass's running totals as its editor shows them, in its order, each with Glass's own label. Glass's lists only the totals the estimate has.";

    public const string GlassReadTotalLabel = "Glass's own label, such as Parts, Paint material, Body, Mechanical, Auxiliary work, Paint, Total (£), VAT (20.00 %) or Amount due (£).";

    public const string GlassReadPosition = "Present when read was position and Glass's answered: the position's figures as Glass's holds them. A figure Glass's does not give is absent.";

    public const string GlassReadOperation = "Glass's code for the position's repair type, such as ER.";

    public const string GlassReadPrice = "The price Glass's holds for the position's repair type and side.";

    public const string GlassReadHours = "The labour hours Glass's holds for the position's repair type and side.";

    public const string GlassReadSide = "L or R; absent for a position with no side.";

    public const string GlassReadPaint = "The position's paint, when it has any: its paint position, its price and its labour time.";

    public const string GlassReadCalculation = "Present when read was calculation and Glass's answered: the Summary of Glass's calculation. Each trade's labour rate, labour time, labour costs and material costs, then Total Labour, Total Material, Repair costs excl. VAT, VAT and Repair costs incl. VAT. A figure Glass's does not give is absent.";

    public const string GlassReadTrade = "Glass's own name for the trade, such as Body, Mechanical, Auxiliary work or Paint.";

    public const string GlassReadVatLabel = "Glass's own label for the VAT line, such as VAT (20.00 %).";

    public const string GlassReadCode = "The glass.* code when Glass's did not answer the read; nothing read is then answered.";

    // The Automation's help in an Engineer's own open Glass's session
    // (R1170, R1182, R1185; design 08 section 5). Not ruled: the sentences
    // the tools gain and the glass.help.* refusals.

    /// <summary>The sentence the three tools that work in an Engineer's session add to their descriptions.</summary>
    public const string GlassHelpWorksIn = "It also works in an Engineer's own open Glass's session on the Case while they have Automation help on; name one by sessionId when there are several.";

    /// <summary>The sentence session_act adds: start, finish and close are the Automation's own session's only.</summary>
    public const string GlassSessionActOwnOnly = "An Engineer's Glass's session is theirs to save or close.";

    public const string GlassSessionId = "Optional: the session to act in, from pegasus_glass_session_get, when the Case has several.";

    public const string GlassSessionOwner = "automation or staff: whose session it is.";

    public const string GlassSessionHelpOn = "An Engineer's session: whether they have Automation help on.";

    public const string GlassSessionWindowConnected = "An Engineer's session: whether their Glass's window is connected to Pegasus.";

    public const string GlassHelpCode = "The glass.help.* code when no session on the Case can be acted in now.";

    public const string GlassHelpSessions = "With glass.help.several, each session that can be acted in.";

    public const string GlassAlsoNew = "An Engineer's session: positions that came onto the estimate from their own window meanwhile.";

    public const string GlassShownInWindow = "An Engineer's session: whether their Glass's window has shown the change yet.";

    /// <summary>
    /// A help refusal: its glass.help.* code, then its sentence. No session
    /// open to the Automation answers the ruled sentence (R1178).
    /// </summary>
    public static string GlassHelpRefusal(string code) => string.Concat(code, ": ", code switch
    {
        GlassSessionHelpPolicy.NoSessionCode => GlassNoSession,
        GlassSessionHelpPolicy.OffCode => "The Engineer's Glass's session on this Case does not have Automation help on.",
        GlassSessionHelpPolicy.SeveralCode => "More than one Glass's session on this Case is open to the Automation; name one by sessionId.",
        GlassSessionHelpPolicy.NotActiveCode => "The Glass's session is no longer open.",
        GlassSessionHelpPolicy.BusyCode => "The Glass's session is busy; try again shortly.",
        GlassSessionHelpPolicy.NoWindowCode => "The Engineer's Glass's window is not connected to Pegasus.",
        GlassSessionHelpPolicy.NotAutomationSessionCode => "That is an Engineer's Glass's session; only they save or close it.",
        _ => "The Glass's session cannot be acted in.",
    });
}
