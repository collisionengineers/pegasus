using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The per-Principal report sending rules (Report Sending SOP v8, 15 October
/// 2026): what Normalize accepts and refuses, and how two values compare.
/// </summary>
public sealed class PrincipalReportSendingRulesTests
{
    private static readonly string ContactId = "00000000-0000-4000-8000-00000000f001";

    private static PrincipalReportSendingRules Rules() => PrincipalReportSendingRules.Default;

    private static ReportSendingRule Rule(
        ReportSendingConditionKind kind,
        string[] values,
        ReportSendingActions? then = null,
        ReportSendingRuleMatch match = ReportSendingRuleMatch.All) =>
        new(match, [new(kind, values)], then ?? new(["cc@example.com"], []));

    private static ReportSendingRulesException Refused(PrincipalReportSendingRules rules) =>
        Assert.Throws<ReportSendingRulesException>(() => PrincipalReportSendingRules.Normalize(rules));

    [Fact]
    public void TheDefaultIsThePlainSopAndNormalizesToItself()
    {
        var normalized = PrincipalReportSendingRules.Normalize(PrincipalReportSendingRules.Default);

        Assert.Equal(PrincipalReportSendingRules.Default, normalized);
        Assert.Null(normalized.SendFromMailbox);
        Assert.Empty(normalized.SendTo);
        Assert.False(normalized.SendToOnly);
        Assert.True(normalized.ReplyAll);
        Assert.Equal(ReportSendingAttachments.Default, normalized.Attach);
        Assert.True(normalized.Attach.FeeNoteSeparate);
        Assert.True(normalized.Attach.ReportImages);
        Assert.False(normalized.GarageFigures);
        Assert.Null(normalized.Hold);
        Assert.Empty(normalized.Rules);
        Assert.Null(normalized.AttachmentName);
    }

    [Fact]
    public void AddressesAreTrimmedDroppedWhenBlankAndUniqueWithoutCase()
    {
        var normalized = PrincipalReportSendingRules.Normalize(Rules() with
        {
            SendFromMailbox = " engineers@collisionengineers.co.uk ",
            SendTo = [" A@example.com ", "", "a@EXAMPLE.com", "b@example.com"],
            Cc = ["c@example.com"],
            NeverCc = ["d@example.com"]
        });

        Assert.Equal("engineers@collisionengineers.co.uk", normalized.SendFromMailbox);
        Assert.Equal(["A@example.com", "b@example.com"], normalized.SendTo.ToArray());
        Assert.Equal("c@example.com", Assert.Single(normalized.Cc));
        Assert.Equal("d@example.com", Assert.Single(normalized.NeverCc));
    }

    [Fact]
    public void ABlankSendFromIsNone()
    {
        Assert.Null(PrincipalReportSendingRules.Normalize(Rules() with { SendFromMailbox = "  " }).SendFromMailbox);
    }

    [Theory]
    [InlineData("not an address")]
    [InlineData("a@")]
    public void AnInvalidAddressIsRefusedWhereverItIsTyped(string address)
    {
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, Refused(Rules() with { SendFromMailbox = address }).Rule);
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, Refused(Rules() with { SendTo = [address] }).Rule);
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, Refused(Rules() with { Cc = [address] }).Rule);
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, Refused(Rules() with { NeverCc = [address] }).Rule);
        var inRule = Refused(Rules() with
        {
            Rules = [Rule(ReportSendingConditionKind.Outcome, ["repairable"], new([address], []))]
        });
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, inRule.Rule);
        Assert.Equal(0, inRule.RuleIndex);
        Assert.Equal(nameof(ReportSendingActions.CcAdd), inRule.Field);
    }

    [Fact]
    public void SendToOnlyNeedsAnAddress()
    {
        Assert.Equal(
            ReportSendingRulesRule.SendToOnlyNeedsAddresses,
            Refused(Rules() with { SendToOnly = true }).Rule);

        var normalized = PrincipalReportSendingRules.Normalize(Rules() with { SendToOnly = true, SendTo = ["a@example.com"] });
        Assert.True(normalized.SendToOnly);
    }

    [Fact]
    public void AnAddressCannotBeAlwaysCopiedAndNeverCopied()
    {
        var error = Refused(Rules() with { Cc = ["a@example.com"], NeverCc = ["A@EXAMPLE.com"] });

        Assert.Equal(ReportSendingRulesRule.AddressInCcAndNeverCc, error.Rule);
    }

    [Fact]
    public void TextsAreTrimmedBlankRemindersDroppedAndLongTextKept()
    {
        var normalized = PrincipalReportSendingRules.Normalize(Rules() with
        {
            Hold = "  WhatsApp the report first.  ",
            Reminders = [" Authorise the garage. ", "", "  "]
        });

        Assert.Equal("WhatsApp the report first.", normalized.Hold);
        Assert.Equal("Authorise the garage.", Assert.Single(normalized.Reminders));

        var longText = new string('x', 501);
        Assert.Equal(longText, PrincipalReportSendingRules.Normalize(Rules() with { Hold = longText }).Hold);
        Assert.Equal(longText, Assert.Single(PrincipalReportSendingRules.Normalize(Rules() with { Reminders = [longText] }).Reminders));
        Assert.Null(PrincipalReportSendingRules.Normalize(Rules() with { Hold = " " }).Hold);
    }

    [Fact]
    public void ContactConditionsTakeContactIdsAndKeepThemInOneForm()
    {
        var upper = ContactId.ToUpperInvariant();
        foreach (var kind in new[] { ReportSendingConditionKind.ClaimSource, ReportSendingConditionKind.Repairer })
        {
            var normalized = PrincipalReportSendingRules.Normalize(Rules() with
            {
                Rules = [Rule(kind, [upper, ContactId, "00000000-0000-4000-8000-00000000f002"])]
            });

            var condition = Assert.Single(Assert.Single(normalized.Rules).If);
            Assert.Equal(kind, condition.Kind);
            Assert.Equal([ContactId, "00000000-0000-4000-8000-00000000f002"], condition.Values.ToArray());

            Assert.Equal(
                ReportSendingRulesRule.UnknownContact,
                Refused(Rules() with { Rules = [Rule(kind, ["Car 2 Go"])] }).Rule);
            Assert.Equal(
                ReportSendingRulesRule.UnknownContact,
                Refused(Rules() with { Rules = [Rule(kind, [Guid.Empty.ToString()])] }).Rule);
        }
    }

    [Fact]
    public void OutcomeConditionsTakeTheFourCodesOnly()
    {
        foreach (var code in new[] { "total_loss", "repairable", "cash_in_lieu", "contract_repair" })
        {
            var normalized = PrincipalReportSendingRules.Normalize(Rules() with
            {
                Rules = [Rule(ReportSendingConditionKind.Outcome, [code])]
            });
            Assert.Equal(code, Assert.Single(Assert.Single(Assert.Single(normalized.Rules).If).Values));
        }

        var error = Refused(Rules() with { Rules = [Rule(ReportSendingConditionKind.Outcome, ["Repairable"])] });
        Assert.Equal(ReportSendingRulesRule.UnknownOutcome, error.Rule);
        Assert.Equal(0, error.RuleIndex);
    }

    /// <summary>A Case type condition takes the types that send a report, by their Core names (operator, 13 October 2026).</summary>
    [Fact]
    public void CaseTypeConditionsTakeTheReportingCaseTypesOnly()
    {
        foreach (var type in new[] { "Inspection", "Audit", "InspectionAndAudit" })
        {
            var normalized = PrincipalReportSendingRules.Normalize(Rules() with
            {
                Rules = [Rule(ReportSendingConditionKind.CaseType, [type])]
            });
            Assert.Equal(type, Assert.Single(Assert.Single(Assert.Single(normalized.Rules).If).Values));
        }

        foreach (var unknown in new[] { "Triage", "audit", "Desktop" })
        {
            var error = Refused(Rules() with { Rules = [Rule(ReportSendingConditionKind.CaseType, [unknown])] });
            Assert.Equal(ReportSendingRulesRule.UnknownCaseType, error.Rule);
            Assert.Equal(0, error.RuleIndex);
        }
    }

    [Fact]
    public void SenderConditionsTakeAddressesAndTextConditionsTakeText()
    {
        var sender = PrincipalReportSendingRules.Normalize(Rules() with
        {
            Rules = [Rule(ReportSendingConditionKind.SenderNot, [" a@example.com "])]
        });
        Assert.Equal("a@example.com", Assert.Single(Assert.Single(Assert.Single(sender.Rules).If).Values));
        Assert.Equal(
            ReportSendingRulesRule.InvalidAddress,
            Refused(Rules() with { Rules = [Rule(ReportSendingConditionKind.SenderNot, ["Luton"])] }).Rule);

        var text = PrincipalReportSendingRules.Normalize(Rules() with
        {
            Rules =
            [
                Rule(ReportSendingConditionKind.Mentions, [" Luton ", "luton"]),
                Rule(ReportSendingConditionKind.ImagesFrom, ["Swinton"]),
                Rule(ReportSendingConditionKind.BodyshopMentions, [" Guardian ", "guardian", "James Claims"])
            ]
        });
        Assert.Equal("Luton", Assert.Single(Assert.Single(text.Rules[0].If).Values));
        Assert.Equal("Swinton", Assert.Single(Assert.Single(text.Rules[1].If).Values));
        Assert.Equal(["Guardian", "James Claims"], Assert.Single(text.Rules[2].If).Values.ToArray());

        var longText = new string('x', 501);
        Assert.Equal(
            longText,
            Assert.Single(Assert.Single(PrincipalReportSendingRules.Normalize(Rules() with
            {
                Rules = [Rule(ReportSendingConditionKind.Mentions, [longText])]
            }).Rules).If).Values.Single());
    }

    /// <summary>A reference prefix is letters only, without the dot (Report Sending SOP v8).</summary>
    [Fact]
    public void ReferencePrefixesAreLettersTrimmedAndUniqueWithoutCase()
    {
        var normalized = PrincipalReportSendingRules.Normalize(Rules() with
        {
            Rules = [Rule(ReportSendingConditionKind.RefPrefix, [" a ", "A", "ab"])]
        });
        Assert.Equal(["a", "ab"], Assert.Single(Assert.Single(normalized.Rules).If).Values.ToArray());

        foreach (var refused in new[] { "a.", "a1", "a b" })
        {
            var error = Refused(Rules() with
            {
                Rules =
                [
                    Rule(ReportSendingConditionKind.Mentions, ["audit"]),
                    Rule(ReportSendingConditionKind.RefPrefix, [refused])
                ]
            });
            Assert.Equal(ReportSendingRulesRule.InvalidRefPrefix, error.Rule);
            Assert.Equal(1, error.RuleIndex);
        }
    }

    /// <summary>A previous job takes no value, so one with none is kept and any typed value is dropped.</summary>
    [Fact]
    public void APreviousJobConditionIsKeptWithNoValue()
    {
        var normalized = PrincipalReportSendingRules.Normalize(Rules() with
        {
            Rules =
            [
                Rule(ReportSendingConditionKind.PreviousJob, []),
                Rule(ReportSendingConditionKind.PreviousJob, ["typed"])
            ]
        });

        Assert.All(normalized.Rules, rule =>
        {
            var condition = Assert.Single(rule.If);
            Assert.Equal(ReportSendingConditionKind.PreviousJob, condition.Kind);
            Assert.Empty(condition.Values);
        });
    }

    /// <summary>
    /// A Rate card or an attach override naming a flag is an action of its
    /// own; an override naming no flag is dropped, so it alone is no action.
    /// </summary>
    [Fact]
    public void ARateCardAndAnAttachOverrideAreActionsAndAnOverrideNamingNoFlagIsDropped()
    {
        var normalized = PrincipalReportSendingRules.Normalize(Rules() with
        {
            Rules =
            [
                Rule(ReportSendingConditionKind.Mentions, ["Luton"], new([], [], RateCard: true)),
                Rule(ReportSendingConditionKind.Mentions, ["Luton"], new([], [], Attach: new(Estimate: true, FigureBreakdown: false))),
                Rule(ReportSendingConditionKind.Mentions, ["Luton"], new(["a@example.com"], [], Attach: new()))
            ]
        });

        Assert.True(normalized.Rules[0].Then.RateCard);
        Assert.Null(normalized.Rules[0].Then.Attach);
        Assert.Equal(new ReportSendingAttachOverride(Estimate: true, FigureBreakdown: false), normalized.Rules[1].Then.Attach);
        Assert.False(normalized.Rules[1].Then.RateCard);
        Assert.Null(normalized.Rules[2].Then.Attach);

        var error = Refused(Rules() with
        {
            Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"], new([], [], Attach: new()))]
        });
        Assert.Equal(ReportSendingRulesRule.EmptyActions, error.Rule);
    }

    /// <summary>
    /// The stored text: the default and a rule with neither a Rate card nor an
    /// attach override write what they wrote before them, and both new fields
    /// round trip.
    /// </summary>
    [Fact]
    public void TheStoredTextOfTheDefaultAndOfARuleWithoutTheNewFieldsIsUnchanged()
    {
        Assert.Equal(
            """{"sendTo":[],"sendToOnly":false,"replyAll":true,"cc":[],"neverCc":[],"attach":{"feeNoteSeparate":true,"estimate":false,"audatex":false,"reportImages":true,"vehicleImagesDocument":false,"figureBreakdown":false},"garageFigures":false,"reminders":[],"rules":[]}""",
            EfOrganizationAdministration.ToReportSendingJson(PrincipalReportSendingRules.Default));

        var plain = Rules() with
        {
            Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"], new([], ["b@example.com"], Remind: "Tell Andy."))]
        };
        Assert.EndsWith(
            ""","rules":[{"match":"All","if":[{"kind":"Mentions","values":["Luton"]}],"then":{"ccAdd":[],"ccRemove":["b@example.com"],"remind":"Tell Andy."}}]}""",
            EfOrganizationAdministration.ToReportSendingJson(plain),
            StringComparison.Ordinal);

        var v8 = Rules() with
        {
            Rules =
            [
                new(
                    ReportSendingRuleMatch.Any,
                    [new(ReportSendingConditionKind.RefPrefix, ["a"]), new(ReportSendingConditionKind.PreviousJob, [])],
                    new([], ["b@example.com"], RateCard: true, Attach: new(Audatex: false)))
            ]
        };
        var json = EfOrganizationAdministration.ToReportSendingJson(v8);
        Assert.EndsWith(
            ""","rules":[{"match":"Any","if":[{"kind":"RefPrefix","values":["a"]},{"kind":"PreviousJob","values":[]}],"then":{"ccAdd":[],"ccRemove":["b@example.com"],"rateCard":true,"attach":{"audatex":false}}}]}""",
            json,
            StringComparison.Ordinal);
        Assert.Equal(v8, EfOrganizationAdministration.ReadReportSending(json));
    }

    [Fact]
    public void EveryRuleNeedsAConditionWithAValueAndAnAction()
    {
        var noConditions = Refused(Rules() with
        {
            Rules = [new(ReportSendingRuleMatch.All, [], new(["a@example.com"], []))]
        });
        Assert.Equal(ReportSendingRulesRule.EmptyRule, noConditions.Rule);

        var noValue = Refused(Rules() with
        {
            Rules = [Rule(ReportSendingConditionKind.Mentions, [" ", ""])]
        });
        Assert.Equal(ReportSendingRulesRule.EmptyCondition, noValue.Rule);

        var noAction = Refused(Rules() with
        {
            Rules =
            [
                Rule(ReportSendingConditionKind.Mentions, ["Luton"], new([], [], "ok")),
                Rule(ReportSendingConditionKind.Mentions, ["Luton"], ReportSendingActions.None)
            ]
        });
        Assert.Equal(ReportSendingRulesRule.EmptyActions, noAction.Rule);
        Assert.Equal(1, noAction.RuleIndex);
    }

    [Fact]
    public void ARuleKeepsItsMatchAndAllItsActions()
    {
        var normalized = PrincipalReportSendingRules.Normalize(Rules() with
        {
            Rules =
            [
                new(
                    ReportSendingRuleMatch.Any,
                    [
                        new(ReportSendingConditionKind.Mentions, ["Luton"]),
                        new(ReportSendingConditionKind.ClaimSource, [ContactId])
                    ],
                    new(["a@example.com"], ["b@example.com"], " Remind. ", " Hold. ", " Stop. "))
            ]
        });

        var rule = Assert.Single(normalized.Rules);
        Assert.Equal(ReportSendingRuleMatch.Any, rule.Match);
        Assert.Equal(2, rule.If.Count);
        Assert.Equal("a@example.com", Assert.Single(rule.Then.CcAdd));
        Assert.Equal("b@example.com", Assert.Single(rule.Then.CcRemove));
        Assert.Equal("Remind.", rule.Then.Remind);
        Assert.Equal("Hold.", rule.Then.Hold);
        Assert.Equal("Stop.", rule.Then.Stop);
        Assert.False(rule.Then.IsEmpty);
    }

    [Fact]
    public void AttachmentNamesTakeOnlyTheKnownTokens()
    {
        var normalized = PrincipalReportSendingRules.Normalize(Rules() with
        {
            AttachmentName = new(" {reg} Initial ", "{reg} Supplementary {ref} {outcome}")
        });
        Assert.Equal("{reg} Initial", normalized.AttachmentName!.First);

        Assert.Equal(
            ReportSendingRulesRule.UnknownNameToken,
            Refused(Rules() with { AttachmentName = new("{reg} Initial", "{vrm} Supplementary") }).Rule);
        Assert.Equal(
            ReportSendingRulesRule.TextRequired,
            Refused(Rules() with { AttachmentName = new("{reg} Initial", " ") }).Rule);
    }

    [Fact]
    public void NormalizeIsIdempotent()
    {
        var once = PrincipalReportSendingRules.Normalize(Rules() with
        {
            SendTo = [" a@example.com "],
            Cc = ["B@example.com"],
            Rules = [Rule(ReportSendingConditionKind.ClaimSource, [ContactId.ToUpperInvariant()])]
        });

        Assert.Equal(once, PrincipalReportSendingRules.Normalize(once));
        Assert.Equal(once.Canonical(), PrincipalReportSendingRules.Normalize(once).Canonical());
    }

    [Fact]
    public void ValuesCompareByContentAndAddressesWithoutCase()
    {
        var one = Rules() with { SendTo = ["a@example.com"], Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"])] };
        var same = Rules() with { SendTo = ["A@EXAMPLE.com"], Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"])] };
        var different = Rules() with { SendTo = ["a@example.com"], Rules = [Rule(ReportSendingConditionKind.Mentions, ["Dunstable"])] };

        Assert.Equal(one, same);
        Assert.Equal(one.GetHashCode(), same.GetHashCode());
        Assert.Equal(one.Canonical(), same.Canonical());
        Assert.NotEqual(one, different);
        Assert.NotEqual(one.Canonical(), different.Canonical());
        Assert.False(one.Equals(null));
    }

    [Fact]
    public void EveryFieldMovesTheCanonicalText()
    {
        var baseline = Rules().Canonical();
        var changes = new[]
        {
            Rules() with { SendFromMailbox = "engineers@collisionengineers.co.uk" },
            Rules() with { SendTo = ["a@example.com"] },
            Rules() with { SendToOnly = true },
            Rules() with { ReplyAll = false },
            Rules() with { Cc = ["a@example.com"] },
            Rules() with { NeverCc = ["a@example.com"] },
            Rules() with { Attach = new(FeeNoteSeparate: false) },
            Rules() with { Attach = new(Estimate: true) },
            Rules() with { Attach = new(Audatex: true) },
            Rules() with { Attach = new(ReportImages: false) },
            Rules() with { Attach = new(VehicleImagesDocument: true) },
            Rules() with { Attach = new(FigureBreakdown: true) },
            Rules() with { GarageFigures = true },
            Rules() with { Hold = "Hold." },
            Rules() with { Reminders = ["Remind."] },
            Rules() with { Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"])] },
            Rules() with { Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"], match: ReportSendingRuleMatch.Any)] },
            Rules() with { Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"], new(["cc@example.com"], [], RateCard: true))] },
            Rules() with { Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"], new(["cc@example.com"], [], Attach: new(Estimate: true)))] },
            Rules() with { Rules = [Rule(ReportSendingConditionKind.Mentions, ["Luton"], new(["cc@example.com"], [], Attach: new(Estimate: false)))] },
            Rules() with { AttachmentName = new("{reg} Initial", "{reg} Supplementary") }
        };

        Assert.All(changes, changed => Assert.NotEqual(baseline, changed.Canonical()));
        Assert.Equal(changes.Length, changes.Select(changed => changed.Canonical()).Distinct().Count());
    }
}
