using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Gaming.Input;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace TBotApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private Gamepad _Gamepad = null;

        private DeviceInformationCollection deviceCollection;
        private DeviceInformation selectedDevice;
        private RfcommDeviceService deviceService;

        public string deviceName = "RNBT-76B7"; // Specify the device name to be selected; You can find the device name from the webb under bluetooth 

        StreamSocket streamSocket = new StreamSocket();


        public MainPage()
        {
            this.InitializeComponent();
            InitializeRfcommServer();
        }

        private async void ControllerConnect_Click(object sender, RoutedEventArgs e)
        {
            Gamepad.GamepadRemoved += Gamepad_GamepadRemoved;
            Gamepad.GamepadAdded += Gamepad_GamepadAdded;

            while (true)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (_Gamepad == null) return;
                    var reading = _Gamepad.GetCurrentReading();
                    LeftStickX.Text = "LeftStickX: " + reading.LeftThumbstickX.ToString("0.0000");
                    LeftStickY.Text = "LeftStickY: " + reading.LeftThumbstickY.ToString("0.0000");
                    RightStickX.Text = "RightStickX: " + reading.RightThumbstickX.ToString("0.0000");
                    RightStickY.Text = "RightStickX: " + reading.RightThumbstickY.ToString("0.0000");

                    Buttons.Text = "";
                    Buttons.Text += (reading.Buttons & GamepadButtons.Y) == GamepadButtons.Y ? " Y " : "";
                    Buttons.Text += (reading.Buttons & GamepadButtons.X) == GamepadButtons.X ? " X " : "";
                    Buttons.Text += (reading.Buttons & GamepadButtons.A) == GamepadButtons.A ? " A " : "";
                    Buttons.Text += (reading.Buttons & GamepadButtons.B) == GamepadButtons.B ? " B " : "";

                    DPAD.Text = "";
                    DPAD.Text += (reading.Buttons & GamepadButtons.DPadDown) == GamepadButtons.DPadDown ? " DPadDown " : "";
                    DPAD.Text += (reading.Buttons & GamepadButtons.DPadLeft) == GamepadButtons.DPadLeft ? " DPadLeft " : "";
                    DPAD.Text += (reading.Buttons & GamepadButtons.DPadRight) == GamepadButtons.DPadRight ? " DPadRight " : "";
                    DPAD.Text += (reading.Buttons & GamepadButtons.DPadUp) == GamepadButtons.DPadUp ? " DPadUp " : "";

                });
                await Task.Delay(TimeSpan.FromMilliseconds(5));
            }

        }


        private async void Gamepad_GamepadRemoved(object sender, Gamepad e)
        {
            _Gamepad = null;

            await Dispatcher.RunAsync(
                               Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                               {
                                   tbConnected.Text = "Controller removed";
                               });
        }

        private async void Gamepad_GamepadAdded(object sender, Gamepad e)
        {
            _Gamepad = e;

            await Dispatcher.RunAsync(
                         Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                         {
                             tbConnected.Text = "Controller added";
                         });
        }

        private async void InitializeRfcommServer()
        {
            try
            {
                string device1 = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort);
                deviceCollection = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(device1);

            }
            catch (Exception exception)
            {
                errorStatus.Visibility = Visibility.Visible;
                errorStatus.Text = exception.Message;
            }
        }

        private async void ConnectToDevice()
        {
            foreach (var item in deviceCollection)
            {
                if (item.Name == deviceName)
                {
                    selectedDevice = item;
                    break;
                }
            }

            if (selectedDevice == null)
            {
                errorStatus.Visibility = Visibility.Visible;
                errorStatus.Text = "Cannot find the device specified; Please check the device name";
                return;
            }
            else
            {
                deviceService = await RfcommDeviceService.FromIdAsync(selectedDevice.Id);

                if (deviceService != null)
                {
                    //connect the socket   
                    try
                    {
                        await streamSocket.ConnectAsync(deviceService.ConnectionHostName, deviceService.ConnectionServiceName);
                    }
                    catch (Exception ex)
                    {
                        errorStatus.Visibility = Visibility.Visible;
                        errorStatus.Text = "Cannot connect bluetooth device:" + ex.Message;
                    }

                }
                else
                {
                    errorStatus.Visibility = Visibility.Visible;
                    errorStatus.Text = "Didn't find the specified bluetooth device";
                }
            }

        }

        private async void SendData_Click(object sender, RoutedEventArgs e)
        {
            if (deviceService != null)
            {
                //send data
                string sendData = messagesent.Text;
                if (string.IsNullOrEmpty(sendData))
                {
                    errorStatus.Visibility = Visibility.Visible;
                    errorStatus.Text = "Please specify the string you are going to send";
                }
                else
                {
                    DataWriter dwriter = new DataWriter(streamSocket.OutputStream);
                    UInt32 len = dwriter.MeasureString(sendData);
                    dwriter.WriteUInt32(len);
                    dwriter.WriteString(sendData);
                    await dwriter.StoreAsync();
                    await dwriter.FlushAsync();
                }

            }
            else
            {
                errorStatus.Visibility = Visibility.Visible;
                errorStatus.Text = "Bluetooth is not connected correctly!";
            }

        }

        private async void ReceiveData_Click(object sender, RoutedEventArgs e)
        {
            // read the data

            DataReader dreader = new DataReader(streamSocket.InputStream);
            uint sizeFieldCount = await dreader.LoadAsync(sizeof(uint));
            if (sizeFieldCount != sizeof(uint))
            {
                return;
            }

            uint stringLength;
            uint actualStringLength;

            try
            {
                stringLength = dreader.ReadUInt32();
                actualStringLength = await dreader.LoadAsync(stringLength);

                if (stringLength != actualStringLength)
                {
                    return;
                }
                string text = dreader.ReadString(actualStringLength);

                message.Text = text;

            }
            catch (Exception ex)
            {
                errorStatus.Visibility = Visibility.Visible;
                errorStatus.Text = "Reading data from Bluetooth encountered error!" + ex.Message;
            }

        }

        private void ConnectDevice_Click(object sender, RoutedEventArgs e)
        {
            ConnectToDevice();
        }
    }
}