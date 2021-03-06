/*
 *	Copyright (C) 2007-2014 ARGUS TV
 *	http://www.argus-tv.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */
using System;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Win32;

using ArgusTV.ServiceAgents;
using System.Windows.Forms;

namespace ArgusTV.WinForms
{
    public static class WinFormsUtility
    {
        public static bool IsVlcInstalled()
        {
            string vlcPath = GetVlcPath();
            return !String.IsNullOrEmpty(vlcPath)
                && File.Exists(vlcPath);
        }

        public static void RunVlc(string fileName)
        {
            string vlcPath = GetVlcPath();
            if (!String.IsNullOrEmpty(vlcPath)
                && File.Exists(vlcPath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(vlcPath,
                    String.Format(CultureInfo.InvariantCulture, "\"{0}\"", fileName));
                startInfo.WorkingDirectory = Path.GetDirectoryName(vlcPath);
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
        }

        public static bool RunStreamPlayer(string rtspUrl, bool isLiveStream)
        {
            string vlcPath = GetVlcPath();
            if (!String.IsNullOrEmpty(vlcPath)
                && File.Exists(vlcPath))
            {
                string mmcPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string playerExe = Path.Combine(mmcPath, "ArgusTV.StreamPlayer.exe");
#if DEBUG
                playerExe = Path.Combine(mmcPath, @"..\..\..\ArgusTV.StreamPlayer\bin\Debug\ArgusTV.StreamPlayer.exe");
#endif
                ProcessStartInfo startInfo = new ProcessStartInfo(playerExe, 
                    String.Format(CultureInfo.InvariantCulture, "\"{0}\" \"{1}\" {2} {3} \"{4}\" \"{5}\" {6} \"{7}\"",
                        vlcPath, ServiceChannelFactories.ServerSettings.ServerName, ServiceChannelFactories.ServerSettings.Port,
                        ServiceChannelFactories.ServerSettings.Transport,
                        ServiceChannelFactories.ServerSettings.UserName, ServiceChannelFactories.ServerSettings.Password,
                        isLiveStream ? "L" : "R", rtspUrl));
                startInfo.WorkingDirectory = Path.GetDirectoryName(vlcPath);
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                return true;
            }
            return false;
        }

        private static string GetVlcPath()
        {
            string vlcPath = ReadRegKeyValue(@"SOFTWARE\Wow6432Node\VideoLAN\VLC");
            if (String.IsNullOrEmpty(vlcPath))
            {
                vlcPath = ReadRegKeyValue(@"SOFTWARE\VideoLAN\VLC");
            }
            return vlcPath;
        }

        private static string ReadRegKeyValue(string keyPath)
        {
            using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                if (regKey != null)
                {
                    return (string)regKey.GetValue(null);
                }
            }
            return null;
        }

        public static void ResizeDataGridViewColumnsForCurrentDpi(DataGridView gridView)
        {
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                ResizeDataGridViewColumns(gridView, graphics.DpiX / 96);
                gridView.ColumnHeadersHeight = (int)(gridView.ColumnHeadersHeight * graphics.DpiY / 96);
            }
        }

        public static void ResizeDataGridViewColumns(DataGridView gridView, float widthFactor)
        {
            foreach (DataGridViewColumn column in gridView.Columns)
            {
                if (column.AutoSizeMode == DataGridViewAutoSizeColumnMode.None
                    || column.AutoSizeMode == DataGridViewAutoSizeColumnMode.NotSet)
                {
                    column.Width = (int)(column.Width * widthFactor);
                }
            }
        }
    }
}
