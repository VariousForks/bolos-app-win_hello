﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.Security.Authentication.Identity.Provider;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.Devices.Enumeration;
using Windows.Devices.SmartCards;
using LedgerHello;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;

namespace Tasks
{
    public sealed class dLock
    {
        public string DeviceId { get; set; }
        public bool isDlockEnabled { get; set; }
        public bool isUsedForLastLogin { get; set; }

        public dLock(string id, bool dLockState, bool lastLogin)
        {
            DeviceId = id;
            isDlockEnabled = dLockState;
            isUsedForLastLogin = lastLogin;
        }
    }
    
    public sealed class authBGTask : IBackgroundTask
    {
        ManualResetEvent opCompletedEvent = null;
        BackgroundTaskDeferral deferral;
        

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            deferral = taskInstance.GetDeferral();

            StorageFolder folder = ApplicationData.Current.LocalFolder;
            //StorageFile logsFile = await folder.CreateFileAsync("BGtask.txt", CreationCollisionOption.OpenIfExists);
            //string txt = "[RUN]: " + DateTime.Now.ToString() + Environment.NewLine;
            //await FileIO.WriteTextAsync(logsFile, txt);

            // This event is signaled when the operation completes
            opCompletedEvent = new ManualResetEvent(false);
            SecondaryAuthenticationFactorAuthentication.AuthenticationStageChanged += OnStageChanged;
            //ShowToastNotification("BG Task Hit!");


            if (taskInstance.TriggerDetails is DeviceWatcherTriggerDetails)
            {
                DeviceWatcherTriggerDetails triggerDetails = (DeviceWatcherTriggerDetails)taskInstance.TriggerDetails;
                
                foreach (DeviceWatcherEvent e in triggerDetails.DeviceWatcherEvents)
                {
                    switch (e.Kind)
                    {
                        case DeviceWatcherEventKind.Add:
                            Debug.WriteLine("[RUN] Add: " + e.DeviceInformation.Id);
                            deferral = taskInstance.GetDeferral();
                            try
                            {
                                SecondaryAuthenticationFactorAuthenticationStageInfo authStageInfo = await SecondaryAuthenticationFactorAuthentication.GetAuthenticationStageInfoAsync();
                                SecondaryAuthenticationFactorAuthenticationStage stage = authStageInfo.Stage;
                                //Debugger.Break();
                                if ((authStageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.WaitingForUserConfirmation)
                                    || (authStageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.CollectingCredential))
                                {
                                    System.Diagnostics.Debug.WriteLine("[RUN] Perform Auth / plug trigger");
                                    Task t = PerformAuthentication();
                                    await t;
                                }
                                //Debugger.Break();
                                if (e.DeviceInformation.Name.Contains("Ledger"))
                                {
                                    Task t = writeConnectedRegisteredDevices();
                                    await t;
                                    
                                }
                            }
                            catch (Exception ex)
                            {
                                
                            }
                            finally
                            {
                                deferral.Complete();
                            }
                            break;

                        //case DeviceWatcherEventKind.Update:
                        //    Debug.WriteLine("[RUN] Update: " + e.DeviceInformationUpdate.Id);
                        //    deferral = taskInstance.GetDeferral();
                        //    
                        //    try
                        //    {
                        //        Task i = writeConnectedRegisteredDevices();
                        //        await i;
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        Debugger.Break();
                        //    }
                        //    finally
                        //    {
                        //        deferral.Complete();
                        //    }
                        //    break;

                        case DeviceWatcherEventKind.Remove:
                            Debug.WriteLine("[RUN] Remove: " + e.DeviceInformationUpdate.Id);
                            deferral = taskInstance.GetDeferral();
                            try
                            {
                                List<dLock> pluggedRegisteredDeviceListBeforeRemove = await readConnectedRegisteredDevices();
                                List<Tuple<string, bool, bool>> pluggedRegisteredDeviceListBeforeRemove_tuple = new List<Tuple<string, bool, bool>>();
                                foreach (dLock device in pluggedRegisteredDeviceListBeforeRemove)
                                {
                                    Tuple<string, bool, bool> newElem = new Tuple<string, bool, bool>(device.DeviceId, device.isDlockEnabled, device.isUsedForLastLogin);
                                    pluggedRegisteredDeviceListBeforeRemove_tuple.Add(newElem);
                                }

                                    IReadOnlyList<SecondaryAuthenticationFactorInfo> registeredDeviceList_removeEvent = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                                    SecondaryAuthenticationFactorDeviceFindScope.User);

                                List<SecondaryAuthenticationFactorInfo> pluggedRegisteredDeviceListAfterRemove = await getConnectedRegisteredDeviceList(registeredDeviceList_removeEvent);
                                
                                foreach (SecondaryAuthenticationFactorInfo deviceToCheck in pluggedRegisteredDeviceListAfterRemove)
                                {
                                    foreach (dLock device in pluggedRegisteredDeviceListBeforeRemove)
                                    {
                                        if (deviceToCheck.DeviceId == device.DeviceId)
                                        {
                                            var t = pluggedRegisteredDeviceListBeforeRemove_tuple.FirstOrDefault(i => i.Item1 == device.DeviceId);
                                            if (t != null)
                                            {
                                                // delete
                                                pluggedRegisteredDeviceListBeforeRemove_tuple.Remove(t);
                                            }                                            
                                        }
                                    }
                                }
                                
                                bool lockDevice = true;
                                string deviceId = "";
                                for (int i = 0; i < pluggedRegisteredDeviceListBeforeRemove_tuple.Count(); i++)
                                {
                                    deviceId = pluggedRegisteredDeviceListBeforeRemove_tuple[i].Item1;
                                    if ((!pluggedRegisteredDeviceListBeforeRemove_tuple[i].Item2) || (!pluggedRegisteredDeviceListBeforeRemove_tuple[i].Item3))
                                    {
                                        lockDevice = false;
                                    }
                                }
                                if (lockDevice)
                                {
                                        Task t = LockDevice(deviceId);
                                        await t;
                                }
                                await writeConnectedRegisteredDevices();
                            }
                            catch (Exception ex)
                            {
                                
                            }
                            finally
                            {
                                deferral.Complete();
                            }
                            break;
                    }
                }
            }
            else
            {
                Debug.WriteLine("[RUN] Unknown trigger");
                deferral = taskInstance.GetDeferral();
                //try
                //{
                //    Task t = writeConnectedRegisteredDevices();
                //    await t;
                //}
                //catch (ConnectedRegisteredDevicesListTxtFileEmpty Ex)
                //{

                //}
                //finally
                //{
                //    deferral.Complete();
                //}           
                
                
            }
            // Wait until the operation completes
            opCompletedEvent.WaitOne();

            deferral.Complete();           
        }
        private async Task writeConnectedRegisteredDevices()
        {
            string NanosATR = "3b00";
            string selector = SmartCardReader.GetDeviceSelector();
            selector += " AND System.Devices.DeviceInstanceId:~~\"Ledger\"";
            byte[] response = { 0 };
            string sw1sw2 = null;
            byte[] deviceDlockState = new byte[1];
            byte[] deviceIdArray = new byte[16];
            string txt = "";

            DeviceInformationCollection readers = await DeviceInformation.FindAllAsync(selector);

            IReadOnlyList<SecondaryAuthenticationFactorInfo> RegisteredDeviceList_addEvent = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                                SecondaryAuthenticationFactorDeviceFindScope.User);            

            List<SecondaryAuthenticationFactorInfo> ConnectedRegisteredDeviceList = await getConnectedRegisteredDeviceList(RegisteredDeviceList_addEvent);
            
            foreach (SecondaryAuthenticationFactorInfo device in ConnectedRegisteredDeviceList)
            {
                foreach (DeviceInformation smartcardreader in readers)
                {
                    SmartCardReader reader = await SmartCardReader.FromIdAsync(smartcardreader.Id);
                    SmartCardReaderStatus readerstatus = await reader.GetStatusAsync();
                    IReadOnlyList<SmartCard> cards = await reader.FindAllCardsAsync();
                    foreach (SmartCard card in cards)
                    {
                        try
                        {
                            IBuffer ATR = await card.GetAnswerToResetAsync();
                            string ATR_str = CryptographicBuffer.EncodeToHexString(ATR);
                            if (ATR_str.Equals(NanosATR))
                            {
                                SmartCardConnection connection = await card.ConnectAsync();
                                response = await Apdu.TransmitApduAsync(connection, Apdu.getDeviceGuidCmdApdu);
                                sw1sw2 = Apdu.ApduResponseParser(response, out response);
                                deviceIdArray = response;
                                string deviceId = BitConverter.ToString(response).Replace("-", "");
                                if (deviceId == device.DeviceId) //update config data with dLockState
                                {
                                     if (device.PresenceMonitoringMode != SecondaryAuthenticationFactorDevicePresenceMonitoringMode.AppManaged)
                                    {
                                        // Skip the device which doesn't need to be monitored in the background task
                                        continue;
                                    }

                                    await device.UpdateDevicePresenceAsync(SecondaryAuthenticationFactorDevicePresence.Present);

                                    response = await Apdu.TransmitApduAsync(connection, Apdu.getDlockStateCmdApdu);
                                    sw1sw2 = Apdu.ApduResponseParser(response, out response);
                                    deviceDlockState = response;

                                    string deviceConfigString = CryptographicBuffer.ConvertBinaryToString(0, device.DeviceConfigurationData);
                                    char[] deviceConfigCharArray = new char[deviceConfigString.Count()];
                                    deviceConfigCharArray = deviceConfigString.ToCharArray();
                                    string deviceConfigStringNew = "";
                                    int count = device.DeviceFriendlyName.Count();
                                    if (deviceDlockState[0] == 0)
                                    {
                                        if (deviceConfigCharArray[35] == '0') // Indicates if device was used for last login
                                        {
                                            deviceConfigStringNew = device.DeviceId + "-0-0-" + device.DeviceFriendlyName + "-" + deviceConfigString.Substring(35 + 1 + count + 1 + 1);
                                        }
                                        else
                                        {
                                            deviceConfigStringNew = device.DeviceId + "-0-1-" + device.DeviceFriendlyName + "-" + deviceConfigString.Substring(35 + 1 + count + 1 + 1);
                                        }
                                    }
                                    else
                                    {
                                        if (deviceConfigCharArray[35] == '0')
                                        {
                                            deviceConfigStringNew = device.DeviceId + "-1-0-" + device.DeviceFriendlyName + "-" + deviceConfigString.Substring(35 + 1 + count + 1 + 1 );
                                        }
                                        else
                                        {
                                            deviceConfigStringNew = device.DeviceId + "-1-1-" + device.DeviceFriendlyName + "-" + deviceConfigString.Substring(35 + 1 + count + 1 + 1);
                                        }    
                                    }
                                    // Get a Ibuffer from combinedDataArray
                                    IBuffer deviceConfigData = CryptographicBuffer.ConvertStringToBinary(deviceConfigString, 0);

                                    await SecondaryAuthenticationFactorRegistration.UpdateDeviceConfigurationDataAsync(device.DeviceId, deviceConfigData);
                                }
                                connection.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            
                        }
                    }
                }
                txt += CryptographicBuffer.ConvertBinaryToString(0, device.DeviceConfigurationData) + Environment.NewLine;
            }            
            StorageFolder folder = ApplicationData.Current.LocalFolder;
            StorageFile ConnectedRegisteredDeviceListFile = await folder.CreateFileAsync("connectedRegisteredDeviceList.txt", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(ConnectedRegisteredDeviceListFile, txt);
        }
        private async Task<List<dLock>> readConnectedRegisteredDevices()
        {
            string readTxt = "";
            StorageFolder readFolder = ApplicationData.Current.LocalFolder;
            StorageFile readFile = await readFolder.CreateFileAsync("connectedRegisteredDeviceList.txt", CreationCollisionOption.OpenIfExists);
            readTxt = await FileIO.ReadTextAsync(readFile);

            string[] lines = readTxt.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            
            List<dLock> pluggedRegisteredDeviceListBeforeRemove = new List<dLock>();

            for (int i = 0; i < lines.Count(); i++)
            {
                string[] info = lines[i].Split('-');

                string deviceGUID = info[0];

                if (info[1] == "0") // info[1] is device dLockState
                {
                    if (info[2] == "0") //// info[2] indicates if device was used for last logon
                    {
                        pluggedRegisteredDeviceListBeforeRemove.Add(new dLock(deviceGUID, false, false));
                    }
                    else
                    {
                        pluggedRegisteredDeviceListBeforeRemove.Add(new dLock(deviceGUID, false, true));
                    }
                }
                else
                {
                    if (info[2] == "0")
                    {
                        pluggedRegisteredDeviceListBeforeRemove.Add(new dLock(deviceGUID, true, false));
                    }
                    else
                    {
                        pluggedRegisteredDeviceListBeforeRemove.Add(new dLock(deviceGUID, true, true));
                    }
                }
            }
            return pluggedRegisteredDeviceListBeforeRemove;
        }        
        private async Task<List<SecondaryAuthenticationFactorInfo>> getConnectedRegisteredDeviceList(IReadOnlyList<SecondaryAuthenticationFactorInfo> devicesToCheck)
        {
            //byte[] deviceConfigurationDataArray;
            string selector = SmartCardReader.GetDeviceSelector();
            selector += " AND System.Devices.DeviceInstanceId:~~\"Ledger\"";
            byte[] response = { 0 };
            string sw1sw2 = null;
            string NanosATR = "3b00";
            byte[] deviceDlockState = new byte[1];
            byte[] deviceIdArray = new byte[16];            

            List<SecondaryAuthenticationFactorInfo> outList = new List<SecondaryAuthenticationFactorInfo>();

            DeviceInformationCollection readers = await DeviceInformation.FindAllAsync(selector);

            foreach (SecondaryAuthenticationFactorInfo device in devicesToCheck)
            {
                //CryptographicBuffer.CopyToByteArray(device.DeviceConfigurationData, out deviceConfigurationDataArray);

                foreach (DeviceInformation smartcardreader in readers)
                {
                    SmartCardReader reader = await SmartCardReader.FromIdAsync(smartcardreader.Id);
                    SmartCardReaderStatus readerstatus = await reader.GetStatusAsync();
                    IReadOnlyList<SmartCard> cards = await reader.FindAllCardsAsync();

                    foreach (SmartCard card in cards)
                    {
                        try
                        {
                            IBuffer ATR = await card.GetAnswerToResetAsync();
                            string ATR_str = CryptographicBuffer.EncodeToHexString(ATR);

                            if (ATR_str.Equals(NanosATR))
                            {
                                SmartCardConnection connection = await card.ConnectAsync();
                                response = await Apdu.TransmitApduAsync(connection, Apdu.getDeviceGuidCmdApdu);
                                sw1sw2 = Apdu.ApduResponseParser(response, out response);
                                deviceIdArray = response;
                                string deviceId = BitConverter.ToString(response).Replace("-", "");
                                if (deviceId == device.DeviceId) //update config data with dLockState and increment counter
                                {
                                    outList.Add(device);
                                }
                                connection.Dispose();
                            }
                        }
                        catch
                        {
                            
                        }
                    }
                }
            }
            return outList;
        }
        private async Task AuthenticateWithSmartCardAsync(SmartCard card)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

            String m_selectedDeviceId = localSettings.Values["SelectedDevice"] as String;
            string m_selectedDeviceFriendlyName = string.Empty;
            string m_selectedDeviceAddDate = string.Empty;
            string deviceConfigString = string.Empty;
            char[] deviceConfigCharArray;
            byte[] deviceIdArray = new byte[16];
            byte[] deviceDlockState = new byte[1];

            bool foundCompanionDevice = false;

            byte[] response = { 0 };
            string sw1sw2 = null;

            SecondaryAuthenticationFactorAuthenticationStageInfo authStageInfo = await SecondaryAuthenticationFactorAuthentication.GetAuthenticationStageInfoAsync();

            if ((authStageInfo.Stage != SecondaryAuthenticationFactorAuthenticationStage.CollectingCredential) && (authStageInfo.Stage != SecondaryAuthenticationFactorAuthenticationStage.WaitingForUserConfirmation))
            {                
                throw new Exception("Unexpected! Stage: " + authStageInfo.Stage);
            }
            //ShowToastNotification("Post Collecting Credential");
            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Post Collecting Credential");
            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                    SecondaryAuthenticationFactorDeviceFindScope.AllUsers);
            if (deviceList.Count == 0)
            {
                //ShowToastNotification("Unexpected exception, device list = 0");
                throw new Exception("Unexpected exception, device list = 0");
            }

            SmartCardConnection connection = await card.ConnectAsync();
            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Connection");            

            response = await Apdu.TransmitApduAsync(connection, Apdu.getDeviceGuidCmdApdu);
            sw1sw2 = Apdu.ApduResponseParser(response, out response);
            deviceIdArray = response;
            string deviceId = BitConverter.ToString(response).Replace("-", "");

            response = await Apdu.TransmitApduAsync(connection, Apdu.getDlockStateCmdApdu);
            sw1sw2 = Apdu.ApduResponseParser(response, out response);
            deviceDlockState = response;

            //string deviceFriendlyName = null;            

            byte[] deviceConfigDataArray = new byte[18]; //16 bytes for GUID and 1 byte for dLockstate
            IBuffer deviceConfigData;
            byte[] deviceConfigurationDataArray;
            //List<byte[]> deviceConfigList = new List<byte[]>();

            

            foreach (SecondaryAuthenticationFactorInfo device in deviceList)
            {
                CryptographicBuffer.CopyToByteArray(device.DeviceConfigurationData, out deviceConfigurationDataArray);
                //deviceConfigList.Add(deviceConfigurationDataArray);

                //deviceFriendlyName = device.DeviceFriendlyName;                
                if (device.DeviceId == deviceId)
                {
                    m_selectedDeviceId = deviceId;
                    m_selectedDeviceFriendlyName = device.DeviceFriendlyName;
                    deviceConfigString = CryptographicBuffer.ConvertBinaryToString(0, device.DeviceConfigurationData);
                    //deviceConfigCharArray = new char[deviceConfigString.Count()];
                    //deviceConfigCharArray = deviceConfigString.ToCharArray();                    
                    int count = device.DeviceFriendlyName.Count();
                    m_selectedDeviceAddDate = deviceConfigString.Substring(35 + 1 + count + 1 + 1);
                    foundCompanionDevice = true;
                    //continue;
                }
            }
            //Debugger.Break();
            if (!foundCompanionDevice)
            {
                throw new CompanionDeviceNotFoundException();
            }
            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Start Nonce APDU sending");
            response = await Apdu.TransmitApduAsync(connection, Apdu.getNonceCmdApdu);
            sw1sw2 = Apdu.ApduResponseParser(response, out response);
            if (sw1sw2 != "9000")
            {
                throw new UnableTogetNonceFromDeviceException();
            }
            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Nonce APDU recieved without error");

            string nonce = BitConverter.ToString(response).Replace("-", "");
            IBuffer svcNonce = CryptographicBuffer.DecodeFromHexString(nonce);

            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Start Authentication");
            SecondaryAuthenticationFactorAuthenticationResult authResult = await SecondaryAuthenticationFactorAuthentication.StartAuthenticationAsync(
                m_selectedDeviceId, svcNonce);
            if (authResult.Status != SecondaryAuthenticationFactorAuthenticationStatus.Started)
            {
                //ShowToastNotification("Unexpected! Could not start authentication!");
                throw new Exception("Unexpected! Could not start authentication! Status: " + authResult.Status);
            }

            byte[] devNonce = { 0 };
            byte[] svcHmac = { 0 };
            byte[] sessNonce = { 0 };

            CryptographicBuffer.CopyToByteArray(authResult.Authentication.ServiceAuthenticationHmac, out svcHmac);
            CryptographicBuffer.CopyToByteArray(authResult.Authentication.SessionNonce, out sessNonce);
            CryptographicBuffer.CopyToByteArray(authResult.Authentication.DeviceNonce, out devNonce);

            byte[] cmd = new byte[Apdu.challengeCmdApdu.Length + svcHmac.Length + sessNonce.Length + devNonce.Length];
            System.Buffer.BlockCopy(Apdu.challengeCmdApdu, 0, cmd, 0, Apdu.challengeCmdApdu.Length);
            System.Buffer.BlockCopy(svcHmac, 0, cmd, Apdu.challengeCmdApdu.Length, svcHmac.Length);
            System.Buffer.BlockCopy(sessNonce, 0, cmd, Apdu.challengeCmdApdu.Length + svcHmac.Length, sessNonce.Length);
            System.Buffer.BlockCopy(devNonce, 0, cmd, Apdu.challengeCmdApdu.Length + svcHmac.Length + sessNonce.Length, devNonce.Length);

            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Send Challenge");

            string str = "\"" + m_selectedDeviceFriendlyName + "\"";

            await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                str,
                SecondaryAuthenticationFactorAuthenticationMessage.DeviceNeedsAttention);

            response = await Apdu.TransmitApduAsync(connection, cmd);
            sw1sw2 = Apdu.ApduResponseParser(response, out response);

            if (sw1sw2 == "6985")
            {
                throw new UnauthorizedUserException();
            }
            else if (sw1sw2 == "6984")
            {
                //ShowToastNotification("Log-in denied by user");
                throw new LogInDeniedByUserException();
            }

            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Response recieved");
            byte[] HMACdk = new byte[32];
            byte[] HMACsk = new byte[32];
            System.Buffer.BlockCopy(response, 0, HMACdk, 0, 32);
            System.Buffer.BlockCopy(response, 32, HMACsk, 0, 32);

            IBuffer deviceHmac = CryptographicBuffer.CreateFromByteArray(HMACdk);
            IBuffer sessionHmac = CryptographicBuffer.CreateFromByteArray(HMACsk);

            SecondaryAuthenticationFactorFinishAuthenticationStatus authStatus = await authResult.Authentication.FinishAuthenticationAsync(deviceHmac,
                sessionHmac);

            if (authStatus != SecondaryAuthenticationFactorFinishAuthenticationStatus.Completed)
            {
                //ShowToastNotification("Unable to complete authentication!");
                System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Unable to complete authentication");
                throw new Exception("Unable to complete authentication!");
            }

            deviceConfigString = "";
            if (deviceDlockState[0] == 0)
            {
                deviceConfigString = deviceId + "-0-1-" + m_selectedDeviceFriendlyName + "-" + m_selectedDeviceAddDate;
            }
            else
            {
                deviceConfigString = deviceId + "-1-1-" + m_selectedDeviceFriendlyName + "-" + m_selectedDeviceAddDate;
            }
            deviceConfigCharArray = new char[deviceConfigString.Count()];
            deviceConfigString.CopyTo(0, deviceConfigCharArray, 0, deviceConfigString.Count()); // because deviceConfigString is readonly
            deviceConfigCharArray[35] = '1'; // to indicate that device was not used for last login
            string deviceConfigStringNew = new string(deviceConfigCharArray);
            //Debugger.Break();
            deviceConfigData = CryptographicBuffer.ConvertStringToBinary(deviceConfigStringNew, 0);
            await SecondaryAuthenticationFactorRegistration.UpdateDeviceConfigurationDataAsync(deviceId, deviceConfigData); //update deviceConfigData

            foreach (SecondaryAuthenticationFactorInfo device in deviceList)
            {
                if (device.DeviceId != deviceId)
                {
                    deviceConfigString = CryptographicBuffer.ConvertBinaryToString(0, device.DeviceConfigurationData);
                    deviceConfigCharArray = new char[deviceConfigString.Count()];
                    deviceConfigString.CopyTo(0, deviceConfigCharArray, 0, deviceConfigString.Count()); // decause deviceConfigString is readonly
                    deviceConfigCharArray[35] = '0'; // to indicate that device was not used for last login
                    deviceConfigStringNew = new string(deviceConfigCharArray);
                    deviceConfigData = CryptographicBuffer.ConvertStringToBinary(deviceConfigStringNew, 0);
                    //Debugger.Break();
                    await SecondaryAuthenticationFactorRegistration.UpdateDeviceConfigurationDataAsync(device.DeviceId, deviceConfigData); //update deviceConfigData
                }
            }
            System.Diagnostics.Debug.WriteLine("[AuthenticateWithSmartCardAsync] Auth completed");
            connection.Dispose();
        }

        private async Task PerformAuthentication()
        {
            string NanosATR = "3b00";
            bool showNotificationFlag = true;

            string selector = SmartCardReader.GetDeviceSelector();
            selector += " AND System.Devices.DeviceInstanceId:~~\"Ledger\"";
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);             

            foreach (DeviceInformation device in devices)
            {
                SmartCardReader reader = await SmartCardReader.FromIdAsync(device.Id);
                SmartCardReaderStatus readerstatus = await reader.GetStatusAsync();
                //System.Diagnostics.Debug.WriteLine("Reader : " + reader.Name + " status : " + readerstatus.ToString());
                IReadOnlyList<SmartCard> cards = await reader.FindAllCardsAsync();

                foreach (SmartCard card in cards)
                {
                    try
                    {
                        IBuffer ATR = await card.GetAnswerToResetAsync();
                        string ATR_str = CryptographicBuffer.EncodeToHexString(ATR);

                        if (ATR_str.Equals(NanosATR))
                        {
                            Task t = AuthenticateWithSmartCardAsync(card);
                            await t;
                        }
                    }
                    catch (CompanionDeviceNotFoundException ex)
                    {
                        ex.DisplayError();
                        break;
                    }
                    catch (UnableTogetNonceFromDeviceException ex)
                    {
                        ex.DisplayError();
                        showNotificationFlag = false;
                        break;
                    }
                    catch (UnauthorizedUserException ex)
                    {
                        ex.DisplayError();
                        await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                            "",
                            SecondaryAuthenticationFactorAuthenticationMessage.UnauthorizedUser);
                        //ShowToastNotification("Wrong Response");
                        showNotificationFlag = false;
                        break;
                    }
                    catch (LogInDeniedByUserException ex)
                    {
                        ex.DisplayError();
                        await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                            "",
                            SecondaryAuthenticationFactorAuthenticationMessage.TryAgain);
                        showNotificationFlag = false;
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[PerformAuthentication] Unhandled Exception / " + ex.Message);
                        showNotificationFlag = false;
                        return;
                    }
                    finally
                    {

                    }                                     
                }
            }
            if (showNotificationFlag)
            {
                var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                var str = loader.GetString("LookingForDevice");

                await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                                str,
                                SecondaryAuthenticationFactorAuthenticationMessage.LookingForDevicePluggedin);
            }
            showNotificationFlag = true;
        } 
        private async Task LockDevice(string deviceGUIDtoLock)
        {
            Debug.WriteLine("[LockDevice]");
            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceInfoList =
                await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(SecondaryAuthenticationFactorDeviceFindScope.User);
            if (deviceInfoList.Count == 0)
            {
                return;
            }
            foreach (SecondaryAuthenticationFactorInfo deviceInfo in deviceInfoList)
            {                
                if (deviceInfo.PresenceMonitoringMode !=
                    SecondaryAuthenticationFactorDevicePresenceMonitoringMode.AppManaged)
                {
                    // Skip the device which doesn't need to be monitored in the background task
                    continue;
                }
                SecondaryAuthenticationFactorDevicePresence state =
                    SecondaryAuthenticationFactorDevicePresence.Absent;
                await deviceInfo.UpdateDevicePresenceAsync(state);
                SecondaryAuthenticationFactorDevicePresenceMonitoringMode mode = deviceInfo.PresenceMonitoringMode;            
            }
        }
        private async Task PresenceMonitor()
        {
            //ShowToastNotification("Presence monitor triggered!");
            System.Diagnostics.Debug.WriteLine("[PresenceMonitor] triggered");
            // Query the devices which can do presence check for the console user
            IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceInfoList =
                await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(SecondaryAuthenticationFactorDeviceFindScope.User);

            if (deviceInfoList.Count == 0)
            {
                return;
            }            

            foreach (SecondaryAuthenticationFactorInfo deviceInfo in deviceInfoList)
            {
                if (deviceInfo.PresenceMonitoringMode !=
                        SecondaryAuthenticationFactorDevicePresenceMonitoringMode.AppManaged)
                {
                    // Skip the device which doesn't need to be monitored in the background task
                    continue;
                }

                await deviceInfo.UpdateDevicePresenceAsync(SecondaryAuthenticationFactorDevicePresence.Present);
            }
        }
        public static void ShowToastNotification(string message)
        {

            ToastTemplateType toastTemplate = ToastTemplateType.ToastImageAndText01;
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(toastTemplate);

            // Set Text
            XmlNodeList toastTextElements = toastXml.GetElementsByTagName("text");
            toastTextElements[0].AppendChild(toastXml.CreateTextNode(message));

            // Set image
            // Images must be less than 200 KB in size and smaller than 1024 x 1024 pixels.
            XmlNodeList toastImageAttributes = toastXml.GetElementsByTagName("image");
            ((XmlElement)toastImageAttributes[0]).SetAttribute("src", "ms-appx:///Images/logo-80px-80px.png");
            ((XmlElement)toastImageAttributes[0]).SetAttribute("alt", "logo");

            // toast duration
            IXmlNode toastNode = toastXml.SelectSingleNode("/toast");
            ((XmlElement)toastNode).SetAttribute("duration", "short");

            // toast navigation
            var toastNavigationUriString = "#/MainPage.xaml?param1=12345";
            var toastElement = ((XmlElement)toastXml.SelectSingleNode("/toast"));
            toastElement.SetAttribute("launch", toastNavigationUriString);

            // Create the toast notification based on the XML content you've specified.
            ToastNotification toast = new ToastNotification(toastXml);

            // Send your toast notification.
            ToastNotificationManager.CreateToastNotifier().Show(toast);

        }        
        async void OnStageChanged(Object sender, SecondaryAuthenticationFactorAuthenticationStageChangedEventArgs args)
        {
            //ShowToastNotification("In StageChanged!" + args.StageInfo.Stage.ToString());
            if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.CheckingDevicePresence)
            {
                Task t = PresenceMonitor();
                t.Wait();
                opCompletedEvent.Set();
                deferral.Complete();
                return;
            }
            if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.WaitingForUserConfirmation)
            {
                //ShowToastNotification("Stage = WaitingForUserConfirmation");
                // This event is happening on a ThreadPool thread, so we need to dispatch to the UI thread.
                // Getting the dispatcher from the MainView works as long as we only have one view.               

                IReadOnlyList<SecondaryAuthenticationFactorInfo> deviceList = await SecondaryAuthenticationFactorRegistration.FindAllRegisteredDeviceInfoAsync(
                            SecondaryAuthenticationFactorDeviceFindScope.User);

                String deviceName = deviceList.ElementAt(deviceList.Count()-1).DeviceFriendlyName;

                string str = "\"" + deviceName + "\"";

                await SecondaryAuthenticationFactorAuthentication.ShowNotificationMessageAsync(
                    str,
                    SecondaryAuthenticationFactorAuthenticationMessage.SwipeUpWelcome);
            }
            else if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.CollectingCredential)
            {
                //ShowToastNotification("Stage = CollectingCredential");
                System.Diagnostics.Debug.WriteLine("[OnStageChanged] Perform Auth / auth trigger");
                try
                {                    
                    Task t = PerformAuthentication(); ;
                    await t;
                    t = writeConnectedRegisteredDevices();
                    await t;
                }
                catch (Exception ex)
                {
                    
                }
                finally
                {
                    deferral.Complete();
                }
            }
            else
            {
                if (args.StageInfo.Stage == SecondaryAuthenticationFactorAuthenticationStage.StoppingAuthentication)
                {
                    SecondaryAuthenticationFactorAuthentication.AuthenticationStageChanged -= OnStageChanged;
                    opCompletedEvent.Set();
                }

                SecondaryAuthenticationFactorAuthenticationStage stage = args.StageInfo.Stage;
            }
        }
    }
    internal class ConnectedRegisteredDevicesListTxtFileEmpty : Exception
    {
        DateTime m_errorTime;
        static ushort s_errorNumber;

        public ConnectedRegisteredDevicesListTxtFileEmpty()
            : base("Companion device not found")
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public ConnectedRegisteredDevicesListTxtFileEmpty(string message)
            : base(message)
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public void DisplayError()
        {
            Debug.WriteLine("ConnectedRegisteredDevicesListTxtFileEmpty n." + s_errorNumber + " Date: " + m_errorTime);
        }
    }
    internal class CompanionDeviceNotFoundException : Exception
    {
        DateTime m_errorTime;
        static ushort s_errorNumber;

        public CompanionDeviceNotFoundException()
            : base("Companion device not found")
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public CompanionDeviceNotFoundException(string message)
            : base(message)
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public void DisplayError()
        {
            Debug.WriteLine("CompanionDeviceNotFoundException n." + s_errorNumber + " Date: " + m_errorTime);
        }
    }
    internal class UnableTogetNonceFromDeviceException : Exception
    {
        DateTime m_errorTime;
        static short s_errorNumber;

        public UnableTogetNonceFromDeviceException()
            : base("Unable to get GUID from device")
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }

        public UnableTogetNonceFromDeviceException(string message)
            : base(message)
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public void DisplayError()
        {
            Debug.WriteLine("UnableTogetNonceFromDeviceException n." + s_errorNumber + " Date: " + m_errorTime);
        }
    }
    internal class LogInDeniedByUserException : Exception
    {
        DateTime m_errorTime;
        static short s_errorNumber;

        public LogInDeniedByUserException()
            : base("Log in denied by user")
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public LogInDeniedByUserException(string message)
            : base(message)
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public void DisplayError()
        {
            Debug.WriteLine("LogInDeniedByUserException n." + s_errorNumber + " Date: " + m_errorTime);
        }
    }
    internal class UnauthorizedUserException : Exception
    {
        DateTime m_errorTime;
        static short s_errorNumber;

        public UnauthorizedUserException()
            : base("Unauthorized User")
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public UnauthorizedUserException(string message)
            : base(message)
        {
            m_errorTime = DateTime.Now;
            s_errorNumber++;
        }
        public void DisplayError()
        {
            Debug.WriteLine("UnauthorizedUserException n." + s_errorNumber + " Date: " + m_errorTime);
        }        
    }
}
