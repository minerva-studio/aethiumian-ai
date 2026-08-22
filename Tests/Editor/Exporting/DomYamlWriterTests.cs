using Aethiumian.AI.Editor.Exporting;
using NUnit.Framework;

namespace Aethiumian.AI.Editor.Tests.Exporting
{
    /// <summary>Tests deterministic ordering and scalar safety of the DOM YAML writer.</summary>
    public sealed class DomYamlWriterTests
    {
        [Test]
        public void Write_PreservesInsertionOrderAndUsesStableIndentation()
        {
            DomMapping document = new DomMapping()
                .Add("first", new DomScalar("value"))
                .Add("items", new DomSequence()
                    .Add(new DomMapping()
                        .Add("id", new DomScalar("one"))
                        .Add("enabled", new DomScalar(true)))
                    .Add(new DomMapping().Add("id", new DomScalar("two"))));

            Assert.That(DomYamlWriter.Write(document), Is.EqualTo(
                "first: value\n" +
                "items:\n" +
                "  - id: one\n" +
                "    enabled: true\n" +
                "  - id: two"));
        }

        [Test]
        public void Write_QuotesAmbiguousStringsAndEscapesControlCharacters()
        {
            DomMapping document = new DomMapping()
                .Add("number", new DomScalar("123"))
                .Add("text", new DomScalar("line\nvalue"))
                .Add("empty", DomNull.Instance);

            Assert.That(DomYamlWriter.Write(document), Is.EqualTo(
                "number: \"123\"\n" +
                "text: \"line\\nvalue\"\n" +
                "empty: null"));
        }
    }
}
