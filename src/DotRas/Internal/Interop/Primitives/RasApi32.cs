﻿using DotRas.Internal.Abstractions.Primitives;
using System;
using System.Text;
using static DotRas.Internal.Interop.NativeMethods;
using static DotRas.Internal.Interop.Ras;

namespace DotRas.Internal.Interop.Primitives {
    internal class RasApi32 : IRasApi32 {
        public int RasClearConnectionStatistics(IntPtr hRasConn) => UnsafeNativeMethods.RasClearConnectionStatistics(hRasConn);

        public int RasConnectionNotification(IntPtr hRasConn, ISafeHandleWrapper hEvent, RASCN dwFlags) => SafeNativeMethods.RasConnectionNotification(hRasConn, hEvent.UnderlyingHandle, dwFlags);

        public int RasEnumConnections(RASCONN[] lpRasConn, ref int lpCb, ref int lpConnections) => SafeNativeMethods.RasEnumConnections(lpRasConn, ref lpCb, ref lpConnections);

        public int RasEnumDevices(RASDEVINFO[] lpRasDevInfo, ref int lpCb, ref int lpcDevices) => SafeNativeMethods.RasEnumDevices(lpRasDevInfo, ref lpCb, ref lpcDevices);

        public int RasDial(ref RASDIALEXTENSIONS lpRasDialExtensions, string lpszPhoneBook, ref RASDIALPARAMS lpRasDialParams, NotifierType dwNotifierType, RasDialFunc2 lpvNotifier, out IntPtr lphRasConn) =>
            UnsafeNativeMethods.RasDial(ref lpRasDialExtensions, lpszPhoneBook, ref lpRasDialParams, dwNotifierType, lpvNotifier, out lphRasConn);

        public int RasGetConnectStatus(IntPtr hRasConn, ref RASCONNSTATUS lpRasConnStatus) => SafeNativeMethods.RasGetConnectStatus(hRasConn, ref lpRasConnStatus);

        public int RasGetCredentials(string lpszPhoneBook, string lpszEntryName, ref RASCREDENTIALS lpCredentials) => SafeNativeMethods.RasGetCredentials(lpszPhoneBook, lpszEntryName, ref lpCredentials);

        public int RasGetEapUserData(IntPtr hToken, string pzPhoneBook, string pzEntry, IntPtr pbEapData, ref int pdwSizeofEapData) => SafeNativeMethods.RasGetEapUserData(hToken, pzPhoneBook, pzEntry, pbEapData, ref pdwSizeofEapData);

        public int RasGetEntryDialParams(string lpszPhoneBook, ref RASDIALPARAMS lpDialParams, out bool lpfPassword) => SafeNativeMethods.RasGetEntryDialParams(lpszPhoneBook, ref lpDialParams, out lpfPassword);

        public int RasGetErrorString(int uErrorValue, StringBuilder lpszErrorString, int cBufSize) => SafeNativeMethods.RasGetErrorString(uErrorValue, lpszErrorString, cBufSize);

        public int RasGetConnectionStatistics(IntPtr hRasConn, ref RAS_STATS lpStatistics) => SafeNativeMethods.RasGetConnectionStatistics(hRasConn, ref lpStatistics);

        public int RasHangUp(IntPtr hRasConn) => UnsafeNativeMethods.RasHangUp(hRasConn);

        public int RasValidateEntryName(string lpszPhoneBook, string lpszEntryName) => SafeNativeMethods.RasValidateEntryName(lpszPhoneBook, lpszEntryName);
    }
}
