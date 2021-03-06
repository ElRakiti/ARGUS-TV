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
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Win32;

using ArgusTV.DataContracts;
using ArgusTV.UI.Process;
using ArgusTV.ServiceAgents;
using System.Xml.Serialization;

namespace ArgusTV.WinForms
{
    public static class ProgramIconUtility
    {
        public static Icon GetIcon(ScheduleType scheduleType, bool isPartOfSeries)
        {
            switch (scheduleType)
            {
                case ScheduleType.Recording:
                    return isPartOfSeries ? Properties.Resources.RecordSeriesIcon : Properties.Resources.RecordIcon;

                case ScheduleType.Alert:
                    return isPartOfSeries ? Properties.Resources.AlertSeriesIcon : Properties.Resources.AlertIcon;

                case ScheduleType.Suggestion:
                    return isPartOfSeries ? Properties.Resources.SuggestionSeriesIcon : Properties.Resources.SuggestionIcon;
            }
            return Properties.Resources.TransparentIcon;
        }

        public static void GetIconAndToolTip(ScheduleType scheduleType, UpcomingCancellationReason cancellationReason,
            bool isPartOfSeries, UpcomingOrActiveProgramsList upcomingRecordings, UpcomingRecording upcomingRecording,
            out Icon icon, out string toolTip)
        {
            toolTip = null;
            bool isCancelled = (cancellationReason != UpcomingCancellationReason.None);
            switch (scheduleType)
            {
                case ScheduleType.Recording:
                    GetRecordingIconAndToolTip(upcomingRecordings, cancellationReason, isPartOfSeries, upcomingRecording,
                        out icon, out toolTip);
                    break;

                case ScheduleType.Alert:
                    icon = isCancelled ? (isPartOfSeries ? Properties.Resources.AlertSeriesCancelledIcon : Properties.Resources.AlertCancelledIcon)
                        : GetIcon(ScheduleType.Alert, isPartOfSeries);
                    break;

                case ScheduleType.Suggestion:
                    icon = isCancelled ? (isPartOfSeries ? Properties.Resources.SuggestionSeriesCancelledIcon : Properties.Resources.SuggestionCancelledIcon)
                        : GetIcon(ScheduleType.Suggestion, isPartOfSeries);
                    break;

                default:
                    icon = Properties.Resources.TransparentIcon;
                    break;
            }
        }

        private static void GetRecordingIconAndToolTip(UpcomingOrActiveProgramsList upcomingRecordings,
            UpcomingCancellationReason cancellationReason, bool isPartOfSeries, UpcomingRecording recording,
            out Icon icon, out string toolTip)
        {
            toolTip = null;
            if (cancellationReason == UpcomingCancellationReason.Manual)
            {
                icon = isPartOfSeries ? Properties.Resources.RecordSeriesCancelledIcon : Properties.Resources.RecordCancelledIcon;
            }
            else if (cancellationReason == UpcomingCancellationReason.PreviouslyRecorded
                || cancellationReason == UpcomingCancellationReason.AlreadyQueued)
            {
                icon = isPartOfSeries ? Properties.Resources.RecordSeriesCancelledHistoryIcon : Properties.Resources.RecordCancelledHistoryIcon;
            }
            else
            {
                if (recording != null && recording.CardChannelAllocation == null)
                {
                    icon = (isPartOfSeries ? Properties.Resources.RecordSeriesInConflictIcon : Properties.Resources.RecordInConflictIcon);
                    toolTip = CreateConflictingProgramsToolTip(upcomingRecordings, recording.ConflictingPrograms);
                }
                else if (recording != null && recording.ConflictingPrograms.Count > 0)
                {
                    icon = (isPartOfSeries ? Properties.Resources.RecordSeriesWithWarningIcon : Properties.Resources.RecordWithWarningIcon);
                    toolTip = CreateConflictingProgramsToolTip(upcomingRecordings, recording.ConflictingPrograms);
                }
                else
                {
                    icon = GetIcon(ScheduleType.Recording, isPartOfSeries);
                }
            }
        }

        private static string CreateConflictingProgramsToolTip(UpcomingOrActiveProgramsList upcomingRecordings, List<Guid> programIds)
        {
            return ProcessUtility.CreateConflictingProgramsToolTip(upcomingRecordings, programIds,
                "Conflicts:", "No card found to record program");
        }
    }
}
