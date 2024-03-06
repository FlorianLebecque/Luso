using SyncoStronbo.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SyncoStronbo {
    internal class Room {

        Dictionary<string,IDevicesTypeManager> devicesManager;

        List<IDevice> devices;

        public Room() {

            devices = new();

            LoadDevicesManager();

        }

        private void LoadDevicesManager() {

            //dynamicaly scan all classes that have the [DevicesTypeManagerAttriburte] to create an instance and manage the devices
            var assembly = Assembly.GetExecutingAssembly();
            foreach (Type type in assembly.GetTypes()) {
                var registerAttribute = type.GetCustomAttribute<DevicesTypeManagerAttriburte>();
                if (registerAttribute != null) {
                    devicesManager.Add(registerAttribute.id, (IDevicesTypeManager)Activator.CreateInstance(type));
                }
            }

            foreach (IDevicesTypeManager deviceTypeManager in devicesManager.Values) {
                deviceTypeManager.OnDeviceConnected += OnNewDevice;
                deviceTypeManager.OnDeviceDisconnected += OnRemoveDevices;
            }
        }

        private void OnNewDevice(Object sender, IDevice device) {
            devices.Add(device);
        }

        private void OnRemoveDevices(Object sender, IDevice device) {
            devices.Remove(device);
        }

    }
}
