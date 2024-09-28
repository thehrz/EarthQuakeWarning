using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using NAudio.CoreAudioApi;

namespace EarthquakeWaring.App.Services
{
    public class VolumeManager : IVolumeManager
    {
        public void SetVolume(int volume)
        {
            using (var deviceEnumerator = new MMDeviceEnumerator())
            {
                using (var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    device.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100.0f;
                }
            }
        }
    }
}
