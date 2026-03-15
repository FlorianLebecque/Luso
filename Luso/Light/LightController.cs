using Microsoft.Maui.Controls.PlatformConfiguration;
using System.Threading.Tasks;

namespace Luso.Light {
    internal class LightController {

        private bool state;

        private bool start;

        private long delay_ticks;

        private Thread lightThread;

        private static LightController _instance;

        public static LightController GetInstance() {
            if(_instance  == null) {
                _instance = new LightController();
            }

            return _instance;
        }

        private LightController() {

            state = false;
            start = false;
            delay_ticks = 1000 * 10000;

            lightThread = new Thread(RunLightStoboscop);
            lightThread.Start();

        }

        public async void Start() {

            PermissionStatus flashPermision = await Permissions.CheckStatusAsync<Permissions.Flashlight>();

            if (flashPermision != PermissionStatus.Granted){
                flashPermision = await Permissions.RequestAsync<Permissions.Flashlight>();
            }

            if (flashPermision != PermissionStatus.Granted)
            {
                start = false;
                return;
            }

            start = true;
        }

        public void Stop() {

            start = false;
        }

        public void SetDelay(long delay_ms) {
            delay_ticks = delay_ms * 10000;
        }

        private void RunLightStoboscop() {

            long time_1 = DateTime.Now.Ticks;

            while (true) {

                if (start) {
                    if (DateTime.Now.Ticks - time_1 > delay_ticks) {
                        time_1 = DateTime.Now.Ticks;

                        if (state) {
                            Flashlight.Default.TurnOnAsync();

                        } else {
                            Flashlight.Default.TurnOffAsync();
                        }

                        state = !state;

                    }
                } else {
                    Flashlight.Default.TurnOffAsync();
                }
            }
        }

        private async void FlashlightSwitch_Toggled(object sender, ToggledEventArgs e) {
           
            await Flashlight.Default.TurnOnAsync();
               
            await Flashlight.Default.TurnOffAsync();

        }

    }


    

}
