using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tripwire.Tests
{
    public class TripwireBaseTests
    {
        TestTripwire _wire;
        List<TripRecord> _records;

        [SetUp]
        public void SetUp()
        {
            _wire = new TestTripwire();
            _records = new List<TripRecord>();
            _wire.OnTripwireActivated += _records.Add;
        }

        [TearDown]
        public void TearDown()
        {
            _wire.OnTripwireActivated -= _records.Add;
            _wire.ThrowOnDisarm = null;
            _wire.Dispose();
        }

        // ---- Arm / Disarm ----

        [Test]
        public void InitialState_IsNotArmedAndNotTripped()
        {
            Assert.That(_wire.IsArmed, Is.False);
            Assert.That(_wire.IsTripped, Is.False);
            Assert.That(_wire.ArmCount, Is.Zero);
        }

        [Test]
        public void Arm_SetsIsArmedAndCallsOnArm()
        {
            _wire.Arm();

            Assert.That(_wire.IsArmed, Is.True);
            Assert.That(_wire.ArmCount, Is.EqualTo(1));
        }

        [Test]
        public void Arm_Twice_CallsOnArmOnlyOnce()
        {
            _wire.Arm();
            _wire.Arm();

            Assert.That(_wire.ArmCount, Is.EqualTo(1));
        }

        [Test]
        public void Disarm_WhenNotArmed_DoesNotCallOnDisarm()
        {
            _wire.Disarm();

            Assert.That(_wire.DisarmCount, Is.Zero);
        }

        [Test]
        public void Disarm_AfterArm_ClearsIsArmedAndCallsOnDisarm()
        {
            _wire.Arm();
            _wire.Disarm();

            Assert.That(_wire.IsArmed, Is.False);
            Assert.That(_wire.DisarmCount, Is.EqualTo(1));
        }

        [Test]
        public void Disarm_Twice_CallsOnDisarmOnlyOnce()
        {
            _wire.Arm();
            _wire.Disarm();
            _wire.Disarm();

            Assert.That(_wire.DisarmCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_Disarms()
        {
            _wire.Arm();
            _wire.Dispose();

            Assert.That(_wire.IsArmed, Is.False);
            Assert.That(_wire.DisarmCount, Is.EqualTo(1));
        }

        [Test]
        public void Rearm_AfterDisarm_CallsOnArmAgain()
        {
            _wire.Arm();
            _wire.Disarm();
            _wire.Arm();

            Assert.That(_wire.IsArmed, Is.True);
            Assert.That(_wire.ArmCount, Is.EqualTo(2));
        }

        [Test]
        public void Arm_WhenOnArmThrows_ReportsSourceErrorAndStaysArmed()
        {
            var ex = new InvalidOperationException("arm failed");
            _wire.ThrowOnArm = ex;

            Assert.DoesNotThrow(() => _wire.Arm());

            Assert.That(_wire.IsArmed, Is.True);
            Assert.That(_records, Has.Count.EqualTo(1));
            var record = _records[0];
            Assert.That(record.EventType, Is.EqualTo(TripEventType.SourceError));
            Assert.That(record.Exception, Is.SameAs(ex));
            Assert.That(record.Context.Message, Is.EqualTo("arm failed"));
            Assert.That(record.Tripwire, Is.SameAs(_wire));
        }

        [Test]
        public void Disarm_WhenOnDisarmThrows_LogsExceptionAndDisarms()
        {
            _wire.Arm();
            _wire.ThrowOnDisarm = new InvalidOperationException("disarm failed");

            LogAssert.Expect(LogType.Exception, new Regex("disarm failed"));
            Assert.DoesNotThrow(() => _wire.Disarm());

            Assert.That(_wire.IsArmed, Is.False);
            Assert.That(_records, Is.Empty);
        }

        // ---- Set (レベル型) ----

        [Test]
        public void Set_True_WhenArmed_TripsAndRecordsTripped()
        {
            _wire.Arm();
            _wire.InvokeSet(true, "bad state");

            Assert.That(_wire.IsTripped, Is.True);
            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.Tripped));
            Assert.That(_records[0].Context.Message, Is.EqualTo("bad state"));
            Assert.That(_records[0].Exception, Is.Null);
        }

        [Test]
        public void Set_SameValueRepeatedly_IsIgnored()
        {
            _wire.Arm();
            _wire.InvokeSet(true);
            _wire.InvokeSet(true);
            _wire.InvokeSet(true);

            Assert.That(_records, Has.Count.EqualTo(1));
        }

        [Test]
        public void Set_False_WhenNotTripped_IsIgnored()
        {
            _wire.Arm();
            _wire.InvokeSet(false);

            Assert.That(_wire.IsTripped, Is.False);
            Assert.That(_records, Is.Empty);
        }

        [Test]
        public void Set_TrueThenFalse_RecordsTrippedThenCleared()
        {
            _wire.Arm();
            _wire.InvokeSet(true);
            _wire.InvokeSet(false, "recovered");

            Assert.That(_wire.IsTripped, Is.False);
            Assert.That(_records, Has.Count.EqualTo(2));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.Tripped));
            Assert.That(_records[1].EventType, Is.EqualTo(TripEventType.Cleared));
            Assert.That(_records[1].Context.Message, Is.EqualTo("recovered"));
        }

        [Test]
        public void Set_WhenNotArmed_IsIgnored()
        {
            _wire.InvokeSet(true);

            Assert.That(_wire.IsTripped, Is.False);
            Assert.That(_records, Is.Empty);
        }

        [Test]
        public void Disarm_KeepsTrippedState()
        {
            _wire.Arm();
            _wire.InvokeSet(true);
            _wire.Disarm();

            Assert.That(_wire.IsTripped, Is.True);
            Assert.That(_records, Has.Count.EqualTo(1), "Disarm で Cleared は記録されない");
        }

        [Test]
        public void Set_AfterDisarm_IsIgnoredAndStateIsKept()
        {
            _wire.Arm();
            _wire.InvokeSet(true);
            _wire.Disarm();
            _wire.InvokeSet(false);

            Assert.That(_wire.IsTripped, Is.True);
            Assert.That(_records, Has.Count.EqualTo(1));
        }

        // ---- Fire (エッジ型) ----

        [Test]
        public void Fire_WhenArmed_RecordsFiredWithoutChangingIsTripped()
        {
            _wire.Arm();
            _wire.InvokeFire("happened");

            Assert.That(_wire.IsTripped, Is.False);
            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.Fired));
            Assert.That(_records[0].Context.Message, Is.EqualTo("happened"));
        }

        [Test]
        public void Fire_EachCallRecordsOnce()
        {
            _wire.Arm();
            _wire.InvokeFire();
            _wire.InvokeFire();
            _wire.InvokeFire();

            Assert.That(_records, Has.Count.EqualTo(3));
            Assert.That(_records, Has.All.Property(nameof(TripRecord.EventType)).EqualTo(TripEventType.Fired));
        }

        [Test]
        public void Fire_WhenNotArmed_IsIgnored()
        {
            _wire.InvokeFire();

            Assert.That(_records, Is.Empty);
        }

        [Test]
        public void Fire_WhileTripped_DoesNotAffectLevelState()
        {
            _wire.Arm();
            _wire.InvokeSet(true);
            _wire.InvokeFire();

            Assert.That(_wire.IsTripped, Is.True);
            Assert.That(_records, Has.Count.EqualTo(2));
        }

        // ---- ReportSourceError ----

        [Test]
        public void ReportSourceError_RecordsSourceErrorWithException()
        {
            _wire.Arm();
            var ex = new Exception("boom");
            _wire.InvokeReportSourceError(ex);

            Assert.That(_records, Has.Count.EqualTo(1));
            Assert.That(_records[0].EventType, Is.EqualTo(TripEventType.SourceError));
            Assert.That(_records[0].Exception, Is.SameAs(ex));
            Assert.That(_records[0].Context.Message, Is.EqualTo("boom"));
            Assert.That(_wire.IsTripped, Is.False);
        }

        // ---- イベント購読 ----

        [Test]
        public void NoSubscribers_DoesNotThrow()
        {
            var wire = new TestTripwire();
            wire.Arm();

            Assert.DoesNotThrow(() =>
            {
                wire.InvokeSet(true);
                wire.InvokeSet(false);
                wire.InvokeFire();
                wire.InvokeReportSourceError(new Exception("x"));
            });

            wire.Dispose();
        }

        [Test]
        public void Unsubscribed_HandlerIsNotCalled()
        {
            _wire.OnTripwireActivated -= _records.Add;
            _wire.Arm();
            _wire.InvokeFire();

            Assert.That(_records, Is.Empty);
        }

        // ---- ToString ----

        [Test]
        public void ToString_ContainsIdAndDescription()
        {
            Assert.That(_wire.ToString(), Is.EqualTo("[test.tripwire] test description"));
        }
    }
}
