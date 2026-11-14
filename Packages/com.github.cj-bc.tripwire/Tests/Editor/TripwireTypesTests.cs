using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tripwire.Tests
{
    public class TripContextTests
    {
        [Test]
        public void Default_HasNullMessageAndTarget()
        {
            TripContext context = default;

            Assert.That(context.Message, Is.Null);
            Assert.That(context.Target, Is.Null);
        }

        [Test]
        public void Constructor_SetsMessageAndTarget()
        {
            var target = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                var context = new TripContext("msg", target);

                Assert.That(context.Message, Is.EqualTo("msg"));
                Assert.That(context.Target, Is.SameAs(target));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ImplicitConversionFromString_SetsMessageOnly()
        {
            TripContext context = "hello";

            Assert.That(context.Message, Is.EqualTo("hello"));
            Assert.That(context.Target, Is.Null);
        }
    }

    public class TripRecordTests
    {
        TestTripwire _wire;
        List<TripRecord> _records;

        [SetUp]
        public void SetUp()
        {
            _wire = new TestTripwire();
            _records = new List<TripRecord>();
            _wire.OnTripwireActivated += _records.Add;
            _wire.Arm();
        }

        [TearDown]
        public void TearDown() => _wire.Dispose();

        [Test]
        public void Record_ReferencesSourceTripwire()
        {
            _wire.InvokeFire();

            Assert.That(_records[0].Tripwire, Is.SameAs(_wire));
        }

        [Test]
        public void Record_CapturesFrameAndTime()
        {
            var before = DateTime.UtcNow;
            var realtimeBefore = Time.realtimeSinceStartup;
            _wire.InvokeFire();
            var after = DateTime.UtcNow;

            var record = _records[0];
            Assert.That(record.Frame, Is.EqualTo(Time.frameCount));
            Assert.That(record.RealtimeSinceStartup, Is.GreaterThanOrEqualTo(realtimeBefore));
            Assert.That(record.TimestampUtc, Is.InRange(before, after));
            Assert.That(record.TimestampUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void ToString_WithMessage_IncludesAllParts()
        {
            _wire.InvokeSet(true, "something wrong");

            var text = _records[0].ToString();

            Assert.That(text, Is.EqualTo(
                $"[Tripwire:Tripped] [test.tripwire] test description (kind=KnownIssue, frame={_records[0].Frame}) — something wrong"));
        }

        [Test]
        public void ToString_WithoutMessage_OmitsMessagePart()
        {
            _wire.InvokeFire();

            var text = _records[0].ToString();

            Assert.That(text, Is.EqualTo(
                $"[Tripwire:Fired] [test.tripwire] test description (kind=KnownIssue, frame={_records[0].Frame})"));
        }
    }
}
