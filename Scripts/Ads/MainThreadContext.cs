using System;
using System.Threading;

namespace IdleRestaurant.Ads
{
    /// <summary>
    /// Yerel SDK geri çağrılarını Unity ana iş parçacığına taşır.
    ///
    /// AdMob ve UMP geri çağrıları Android'de Unity ana iş parçacığında
    /// GELMEZ; oradan Unity API'sine dokunmak çöker. Nesne ana iş parçacığında
    /// oluşturulmalı: o anki SynchronizationContext (Unity'nin kendi
    /// bağlamı) yakalanır ve sonraki çağrılar oraya gönderilir.
    /// </summary>
    internal sealed class MainThreadContext
    {
        private readonly SynchronizationContext _context = SynchronizationContext.Current;

        /// <summary>Zaten ana iş parçacığındaysa hemen, değilse bir sonraki karede çalıştırır.</summary>
        public void Run(Action action)
        {
            if (_context == null || SynchronizationContext.Current == _context)
            {
                action();
                return;
            }

            _context.Post(_ => action(), null);
        }
    }
}
