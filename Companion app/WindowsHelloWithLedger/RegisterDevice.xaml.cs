﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SmartCards;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Authentication.Identity.Provider;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace WindowsHelloWithLedger
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class RegisterDevice : Page
    {
        private DeviceWatcher deviceWatcher = null;
        private TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> handlerRemoved = null;

        //internal IEnumerable<DeviceInformationDisplay> ResultCollection { get; private set; }

        public RegisterDevice()
        {
            this.InitializeComponent();
            StartWatcher();
            //string text = NameYourDevice.Text;
        }

        private void StartWatcher()
        {

            string selector = SmartCardReader.GetDeviceSelector();
            selector += " AND System.Devices.DeviceInstanceId:~~\"Ledger\"";

            deviceWatcher = DeviceInformation.CreateWatcher(selector,null);

            handlerRemoved = new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>( async (watcher, deviceInfoUpdate) => 
            {
                StopWatcher();
                await Removed();
            });
            deviceWatcher.Removed += handlerRemoved;
            deviceWatcher.Start();
        }

        private void StopWatcher()
        {
            if (null != deviceWatcher)
            {
                // First unhook all event handlers except the stopped handler. This ensures our
                // event handlers don't get called after stop, as stop won't block for any "in flight" 
                // event handler calls.  We leave the stopped handler as it's guaranteed to only be called
                // once and we'll use it to know when the query is completely stopped. 
                deviceWatcher.Removed -= handlerRemoved;

                if (DeviceWatcherStatus.Started == deviceWatcher.Status ||
                    DeviceWatcherStatus.EnumerationCompleted == deviceWatcher.Status)
                {
                    deviceWatcher.Stop();
                }
            }           
        }

        private async Task Removed()
        {
            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(SecondaryAuthenticationFactorDeviceFindScope.AllUsers);
            if (deviceList.Count > 0)
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.Frame.Navigate(typeof(MainPage), "false");
                });
                //this.Frame.Navigate(typeof(MainPage), "false");
            }
            else
            {
                //waitingForDevice.FrameProperty.
                //((Page)waitingForDevice).Frame.Navigate(typeof(waitingForDevice));
                //((Page)RegisterDevice).Frame.Navigate(typeof(waitingForDevice));

                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     this.Frame.Navigate(typeof(waitingForDevice));
                 });

                //this.Frame.Navigate(typeof(waitingForDevice));
            }
        }

        private void Assistance_pointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 0);
            if (sender is StackPanel)
            {
                ((StackPanel)((Grid)((StackPanel)sender).Parent).Children.ElementAt(2)).Visibility = Visibility.Visible;
            }
            else
            {

            }
            //((Image)((Grid)((Image)e.OriginalSource).Parent).Children.ElementAt(2)).Visibility = Visibility.Visible;
            //((Image)e.OriginalSource).Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Button-assistance-select.png"));
            e.Handled = true;
        }
        private void Assistance_pointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            if (sender is StackPanel)
            {
                ((StackPanel)((Grid)((StackPanel)sender).Parent).Children.ElementAt(2)).Visibility = Visibility.Collapsed;
            }
            else
            {

            }
            //((Image)((Grid)e.OriginalSource).Children.ElementAt(1)).Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Button-assistance.png"));
            e.Handled = true;
        }

        private async void Assistance_Click(object sender, TappedRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            string uriToLaunch = @"http://www.ledgerwallet.com";
            var uri = new Uri(uriToLaunch);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private void StackCancel_pointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 0);
            if (sender is StackPanel)
            {
                ((StackPanel)sender).Children.ElementAt(1).Visibility = Visibility.Visible;
            }
            else
            {

            }
        }

        private void StackCancel_pointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            if (sender is StackPanel)
            {
                ((StackPanel)sender).Children.ElementAt(1).Visibility = Visibility.Collapsed;
            }
            else
            {

            }
        }

        private void StackRegister_pointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 0);
            if (sender is StackPanel)
            {
                ((StackPanel)sender).Children.ElementAt(1).Visibility = Visibility.Visible;
            }
            else
            {

            }
        }

        private void StackRegsiter_pointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            if (sender is StackPanel)
            {
                ((StackPanel)sender).Children.ElementAt(1).Visibility = Visibility.Collapsed;
            }
            else
            {

            }
        }

        private void StackCancel_tapped(object sender, TappedRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            this.Frame.Navigate(typeof(MainPage));
        }

        private async void StackRegsiter_tapped(object sender, TappedRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            if ((sender is StackPanel) && (e.OriginalSource is TextBlock))
            {
                ((Grid)((StackPanel)sender).Parent).Children.ElementAt(3).Visibility = Visibility.Visible;
                ((StackPanel)sender).Children.ElementAt(1).Visibility = Visibility.Collapsed;
            }
            string deviceFriendlyName = NameYourDevice.Text;
            try
            {
                await CommomMethods.RegisterDevice_Click(deviceFriendlyName);
            }
            catch
            {
                IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(SecondaryAuthenticationFactorDeviceFindScope.AllUsers);
                if (deviceList.Count > 0)
                {
                    this.Frame.Navigate(typeof(MainPage), "false");
                }
                else
                {
                    this.Frame.Navigate(typeof(waitingForDevice));
                }
            }
            finally
            {
                this.Frame.Navigate(typeof(MainPage), "false");
            }
        }
    }    
}
