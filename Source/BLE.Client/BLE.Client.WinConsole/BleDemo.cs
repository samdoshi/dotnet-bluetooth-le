﻿using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Extensions;
using Plugin.BLE.Extensions;
using Plugin.BLE.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;

namespace BLE.Client.WinConsole
{
    internal class BleDemo
    {
        private readonly IBluetoothLE bluetoothLE;
        public IAdapter Adapter { get; }
        private readonly Action<string, object[]>? writer;
        private readonly List<IDevice> discoveredDevices;
        private readonly IDictionary<Guid, IDevice> connectedDevices;
        private bool scanningDone = false;

        public BleDemo(Action<string, object[]>? writer = null)
        {
            discoveredDevices = new List<IDevice>();
            connectedDevices = new ConcurrentDictionary<Guid, IDevice>();
            bluetoothLE = CrossBluetoothLE.Current;
            Adapter = CrossBluetoothLE.Current.Adapter;
            this.writer = writer;
        }        

        private void Write(string format, params object[] args)
        {
            writer?.Invoke(format, args);
        }

        public IDevice ConnectToKnown(Guid id)
        {
            IDevice dev = Adapter.ConnectToKnownDeviceAsync(id).Result;
            connectedDevices[id] = dev;
            return dev;
        }

        public IDevice ConnectToKnown(string bleaddress)
        {
            var id = bleaddress.ToBleDeviceGuid();
            return ConnectToKnown(id);
        }

        public async Task DoTheScanning(ScanMode scanMode = ScanMode.LowPower, int time_ms = 2000)
        {

            if (!bluetoothLE.IsOn)
            {
                Write("Bluetooth is not On - it is {0}", bluetoothLE.State);
                return;
            }
            Write("Bluetooth is on");
            Write("Scanning now for " + time_ms + " ms...");            
            var cancellationTokenSource = new CancellationTokenSource(time_ms);
            discoveredDevices.Clear();

            Adapter.DeviceDiscovered += (s, a) =>
            {
                var dev = a.Device;
                Write("DeviceDiscovered: {0} with Name = {1}", dev.Id.ToHexBleAddress(), dev.Name);
                discoveredDevices.Add(a.Device);
            };
            Adapter.ScanMode = scanMode;
            await Adapter.StartScanningForDevicesAsync(cancellationToken: cancellationTokenSource.Token);
            scanningDone = true;
        }

        private void WriteAdvertisementRecords(IDevice device)
        {
            if (device.AdvertisementRecords is null)
            {
                Write("{0} {1} has no AdvertisementRecords...", device.Name, device.State);
                return;
            }
            Write("{0} {1} with {2} AdvertisementRecords", device.Name, device.State, device.AdvertisementRecords.Count);
            foreach (var ar in device.AdvertisementRecords)
            {
                switch (ar.Type)
                {
                    case AdvertisementRecordType.CompleteLocalName:
                        Write(ar.ToString() + " = " + Encoding.UTF8.GetString(ar.Data));
                        break;
                    default:
                        Write(ar.ToString());
                        break;
                }
            }
        }

        /// <summary>
        /// Connect to a device with a specific name
        /// Assumes that DoTheScanning has been called and that the device is advertising 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<IDevice?> ConnectTest(string name)
        {
            if (!scanningDone)
            {
                Write("ConnectTest({0}) Failed - Call the DoTheScanning() method first!");
                return null;
            }
            Thread.Sleep(10);
            foreach(var device in discoveredDevices)
            {
                if (device.Name.Contains(name))
                {
                    await Adapter.ConnectToDeviceAsync(device);                    
                    return device;
                }
            }
            return null;
        }

        public void ShowGetSystemConnectedOrPairedDevices()
        {
            IReadOnlyList<IDevice>  devs = Adapter.GetSystemConnectedOrPairedDevices();
            Write("GetSystemConnectedOrPairedDevices found {0} devices.", devs.Count);
            foreach(var dev in devs)
            {
                Write("{0}: {1}", dev.Id.ToHexBleAddress(), dev.Name);
            }
        }

        /// <summary>
        /// This demonstrates a bug where the known services is not cleared at disconnect (2023-11-03)
        /// </summary>
        /// <param name="bleaddress">12 hex char ble address</param>
        public async Task ShowNumberOfServices(string bleaddress)
        {
            Write("Connecting to device with address = {0}", bleaddress);            
            IDevice dev = await Adapter.ConnectToKnownDeviceAsync(bleaddress.ToBleDeviceGuid()) ?? throw new Exception("null");
            string name = dev.Name;
            Write("Connected to {0} {1} {2}", name, dev.Id.ToHexBleAddress(), dev.State);
            Write("Calling dev.GetServicesAsync()...");
            var services = await dev.GetServicesAsync();
            Write("Found {0} services", services.Count);
            Thread.Sleep(1000);
            Write("Disconnecting from {0} {1}", name, dev.Id.ToHexBleAddress());
            await Adapter.DisconnectDeviceAsync(dev);
            Thread.Sleep(1000);
            Write("ReConnecting to device {0} {1}...", name, dev.Id.ToHexBleAddress());
            await Adapter.ConnectToDeviceAsync(dev);
            Write("Connect Done.");
            Thread.Sleep(1000);
            Write("Calling dev.GetServicesAsync()...");
            services = await dev.GetServicesAsync();
            Write("Found {0} services", services.Count);
            await Adapter.DisconnectDeviceAsync(dev);
            Thread.Sleep(1000);
        }

        internal Task Disconnect(IDevice dev)
        {
            return Adapter.DisconnectDeviceAsync(dev);
        }
    }
}
