using Aethiumian.AI.Editor.Exporting;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;

namespace Aethiumian.AI.Editor.Tests.Exporting
{
    /// <summary>Focused tests for deterministic native JSON rendering of the semantic DOM.</summary>
    public sealed class DomJsonWriterTests
    {
        [Test]
        public void Write_PreservesInsertionOrderAndNativeJsonTypes()
        {
            DomMapping document = new DomMapping()
                .Add("first", new DomScalar("value"))
                .Add("enabled", new DomScalar(true))
                .Add("items", new DomSequence()
                    .Add(new DomScalar(3))
                    .Add(DomNull.Instance));

            string json = DomJsonWriter.Write(document);
            JObject parsed = JObject.Parse(json);

            Assert.That(json, Is.EqualTo("{\"first\":\"value\",\"enabled\":true,\"items\":[3,null]}"));
            Assert.That(parsed["first"]?.Type, Is.EqualTo(JTokenType.String));
            Assert.That(parsed["enabled"]?.Type, Is.EqualTo(JTokenType.Boolean));
            Assert.That(parsed["items"]?[0]?.Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(parsed["items"]?[1]?.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void Write_UsesCompactNumericArraysForUnityMathValues()
        {
            DomMapping document = new DomMapping()
                .Add("vector", new DomScalar(new UnityEngine.Vector3(1f, 2f, 3f)))
                .Add("color", new DomScalar(new UnityEngine.Color(0.1f, 0.2f, 0.3f, 1f)));

            JObject parsed = JObject.Parse(DomJsonWriter.Write(document));

            Assert.That(parsed["vector"]?.Type, Is.EqualTo(JTokenType.Array));
            Assert.That(parsed["vector"]?.Values<float>(), Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(parsed["color"]?.Type, Is.EqualTo(JTokenType.Array));
            Assert.That(parsed["color"]?.Values<float>(), Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f, 1f }));
        }

        [Test]
        public void Write_RejectsNonFiniteFloatingPointValues()
        {
            DomMapping document = new DomMapping().Add("value", new DomScalar(float.NaN));

            Assert.That(() => DomJsonWriter.Write(document),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Write_EscapesStringsUsingJsonRules()
        {
            DomMapping document = new DomMapping().Add("text", new DomScalar("line\nvalue\""));

            Assert.That(DomJsonWriter.Write(document), Is.EqualTo("{\"text\":\"line\\nvalue\\\"\"}"));
        }
    }
}
