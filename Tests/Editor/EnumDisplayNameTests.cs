using Genix.Areas;
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
