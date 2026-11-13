using System.Threading;
using Tripwire;
using R3;

namespace Tripwire.R3
{
    public static class TripwireR3Extension
    {
        public static Observable<TripRecord> AsObservable(this TripwireBase wire, CancellationToken cancelationToken)
            => Observable.FromEvent<TripRecord>(h => wire.OnTripwireActivated += h,
                                                h => wire.OnTripwireActivated -= h, cancelationToken);
    }
}
