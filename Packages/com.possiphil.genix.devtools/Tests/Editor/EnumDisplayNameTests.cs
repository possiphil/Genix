using Genix.Areas;
using Genix.Editor.Diagnostics;
using Genix.Extensions;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class EnumDisplayNameTests
    {
        private enum FormattingExample
        {
            A,
            _LEADING_VALUE,
            TRAILING_,
            A__B,
            XMLParser
        }

        [TestCase(RejectionReason.OverlapsFixed, "Overlaps Fixed")]
        [TestCase(RejectionReason.TooCloseToGenerated, "Too Close To Generated")]
        [TestCase(SurfaceDiscoveryMode.NearSfsBoundaries, "Near Sfs Boundaries")]
        public void CamelCaseEnumNamesBecomeReadable(System.Enum value, string expected)
        {
            Assert.That(value.ToDisplayName(), Is.EqualTo(expected));
        }

        [Test]
        public void LongRejectionReasonUsesConciseDesignerLabel()
        {
            RejectionReason value = RejectionReason.AssetRelationAnchorCapacityReached;

            Assert.That(value.ToDisplayName(), Is.EqualTo("Anchor Capacity Reached"));
        }

        [Test]
        public void LegacyLongRejectionLabelIsShortenedWhenDisplayed()
        {
            Assert.That(
                RejectionReasonGuidance.GetDisplayName("Asset Relation Anchor Capacity Reached"),
                Is.EqualTo("Anchor Capacity Reached"));
        }

        [Test]
        public void EveryLegacyRejectionLabelResolvesToCurrentDisplayName()
        {
            foreach (RejectionReason reason in System.Enum.GetValues(typeof(RejectionReason)))
            {
                string legacyName = EnumDisplayNameExtensions.ToDisplayName((System.Enum)reason);

                Assert.That(
                    RejectionReasonGuidance.GetDisplayName(legacyName),
                    Is.EqualTo(reason.ToDisplayName()),
                    reason.ToString());
            }
        }

        [Test]
        public void EveryVisibleRejectionReasonHasActionableGuidance()
        {
            foreach (RejectionReason reason in System.Enum.GetValues(typeof(RejectionReason)))
            {
                if (reason == RejectionReason.None)
                    continue;

                Assert.That(
                    RejectionReasonGuidance.GetAdvice(reason),
                    Is.Not.Empty,
                    reason.ToString());
            }
        }

        [TestCase(FormattingExample.A, "A")]
        [TestCase(FormattingExample._LEADING_VALUE, "Leading Value")]
        [TestCase(FormattingExample.TRAILING_, "Trailing")]
        [TestCase(FormattingExample.A__B, "A B")]
        [TestCase(FormattingExample.XMLParser, "Xml Parser")]
        public void SeparatorAndAcronymEdgeCasesRemainReadable(System.Enum value, string expected)
        {
            Assert.That(value.ToDisplayName(), Is.EqualTo(expected));
        }
    }
}
