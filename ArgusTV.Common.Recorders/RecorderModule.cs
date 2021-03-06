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
using System.Reflection;
using ArgusTV.Common.Logging;
using ArgusTV.Common.StaThreadSyncronizer;
using ArgusTV.DataContracts;
using ArgusTV.DataContracts.Tuning;
using Nancy;
using Nancy.ModelBinding;

namespace ArgusTV.Common.Recorders
{
    public abstract class RecorderModule<TService> : NancyModule
        where TService : RecorderServiceBase, new()
    {
        private static StaSynchronizationContext _staContext;

        public static TService Service { get; set; }

        static RecorderModule()
        {
            Service = new TService();
            _staContext = new StaSynchronizationContext("Recorder");
        }

        public static void DisposeModule() 
        {
            _staContext.Dispose();
            _staContext = null;
            Service.Dispose();
            Service = null;
        }

        public RecorderModule()
            : base("Recorder")
        {
            this.OnError.AddItemToStartOfPipeline((context, ex) =>
            {
                EventLogger.WriteEntry(ex);
                return null;
            });

            // Ping Recorder service.
            //
            // Returns the version of the API on the recorder, which should be Constants.RecorderApiVersion.
            Get["/Ping"] = p =>
            {
                int result = -1;
                _staContext.Send((s) => { result = Service.Ping(); }, null);
                return new
                {
                    result = Constants.RecorderApiVersion
                };
            };

            // Get the server's MAC address(es).  These can be stored on the client after a successful
            // connect and later used to re-connect with wake-on-lan.
            //
            // Returns an array containing one or more MAC addresses in HEX string format (e.g. "A1B2C3D4E5F6").
            Get["/MacAddresses"] = p =>
            {
                List<String> result = null;
                _staContext.Send((s) => { result = Service.GetMacAddresses(); }, null);
                return new
                {
                    result = result
                };
            };

            // Ask the recorder to initialize by registering itself over the Recorder callback's
            // RegisterRecorder method.
            //
            // recorderId - The unique ID of this recorder.
            //
            // Post-data is an object with:
            //   schedulerBaseUrl - The callback URL for the Recorder to communicate with the Scheduler.
            Put["/Initialize/{recorderId}"] = p =>
            {
                var args = this.Bind<InitializeArguments>();

                _staContext.Send((s) => { Service.Initialize(p.recorderId, args.schedulerBaseUrl); }, null);
                return HttpStatusCode.OK;
            };

            // Ask the recorder to allocate a (virtual) card for a channel.  The previously allocated
            // cards are also passed in, so the implementation must take into account that these cards
            // are no longer available when this call is made.  Note that the implementation must *not*
            // worry about cards being actually free at the moment the call is made!  This is purely a
            // theoretical calculation that is used by ARGUS TV to manage its upcoming recordings.
            //
            //   channel - The channel to allocate.
            //   alreadyAllocated - All previously allocated channels/cards.
            //   useReversePriority - Use reverse cards priority to avoid conflicts with live streaming.
            //
            // Returns the unique card ID of the card that can record this channel, or null if no free card was found.
            Put["/AllocateCard"] = p =>
            {
                var args = this.Bind<AllocateCardArguments>();

                string result = null;
                _staContext.Send((s) => { result = Service.AllocateCard(args.channel, args.alreadyAllocated, args.useReversePriority); }, null);
                return new
                {
                    result = result
                };
            };

            // Tell the recorder to actually start a recording on the given card.  The implementation
            // must call /NewRecording on the Recorder callback service when the recording actually
            // starts.  If the recording can't start for some reason /Recording/StartFailed must be called.
            // In case the recording ends (prematurely or on time) /EndRecording must be called.
            // IMPORTANT: If the suggested relative path and filename was used the recorder should
            // return 'false' to /EndRecording's 'okToMoveFile'!
            //
            // Post-data is an object with:
            //   schedulerBaseUrl - The callback URL for the Recorder to communicate with the Scheduler.
            //   channelAllocation - The card allocation for the channel.
            //   startTimeUtc - The actual time to start the recording (UTC).
            //   stopTimeUtc - The actual time to stop the recording (UTC).
            //   recordingProgram - The program to record.
            //   suggestedBaseFileName - The suggested relative path and filename (without extension) of the recording file.
            //
            // Returns a boolean indicating the recording was initiated succesfully.
            Post["/Recording/Start"] = p =>
            {
                var args = this.Bind<StartRecordingArguments>();

                bool result = false;
                _staContext.Send((s) => { result = Service.StartRecording(args.schedulerBaseUrl, args.channelAllocation, args.startTimeUtc, args.stopTimeUtc, args.recordingProgram, args.suggestedBaseFileName); }, null);
                return new
                {
                    result = result
                };
            };

            // Validate a recording is still running, and update its actual stop time.
            //
            // Post-data is an object with:
            //   channelAllocation - The card allocation for the channel.
            //   recordingProgram - The program being recorded.
            //   stopTimeUtc - The up-to-date stop time (UTC).
            //
            // Returns true if the recording was still running (and its stop time was succesfully updated), false if there was a problem or the recording is not running.
            Put["/Recording/ValidateAndUpdate"] = p =>
            {
                var args = this.Bind<ValidateAndUpdateRecordingArguments>();

                bool result = false;
                _staContext.Send((s) => { result = Service.ValidateAndUpdateRecording(args.channelAllocation, args.recordingProgram, args.stopTimeUtc); }, null);
                return new
                {
                    result = result
                };
            };

            // Tell the recorder to abort the recording of a program.  The implementation must call
            // EndRecording() on the Recorder callback service.
            //
            // Post-data is an object with:
            //   schedulerBaseUrl - The callback URL for the Recorder to communicate with the Scheduler.
            //   recordingProgram - The program being recorded.
            //
            // Returns true if the recording was found and aborted.
            Put["/Recording/Abort"] = p =>
            {
                var args = this.Bind<AbortRecordingArguments>();

                bool result = false;
                _staContext.Send((s) => { result = Service.AbortRecording(args.schedulerBaseUrl, args.recordingProgram); }, null);
                return new
                {
                    result = result
                };
            };

            // Retrieves the recording shares of the recorder.
            Get["/RecordingShares"] = p =>
            {
                List<string> result = null;
                _staContext.Send((s) => { result = Service.GetRecordingShares(); }, null);
                return new
                {
                    result = result
                };
            };

            // Retrieves the timeshift shares of the recorder.
            Get["/TimeshiftShares"] = p =>
            {
                List<string> result = null;
                _staContext.Send((s) => { result = Service.GetTimeshiftShares(); }, null);
                return new
                {
                    result = result
                };
            };

            #region Live Streaming

            // Tune to a channel, and get a live stream to that channel.
            //
            // Post-data is an object with:
            //   channel - The channel to tune to.
            //   upcomingRecordingAllocation - The allocation of the next upcoming recording, or null if there isn't one.
            //   stream - The live stream in case of retuning an existing stream, or null for a new one.
            //
            // Returns a LiveStreamResult value to indicate success or failure.
            Post["/Live/Tune"] = p =>
            {
                var args = this.Bind<TuneLiveStreamArguments>();
                var stream = args.stream;

                LiveStreamResult result = LiveStreamResult.UnknownError;
                _staContext.Send((s) => { result = Service.TuneLiveStream(args.channel, args.upcomingRecordingAllocation, ref stream); }, null);
                return new
                {
                    result = result,
                    stream = stream
                };
            };

            // Tell the recorder we are still showing this stream and to keep it alive. Call this every 30 seconds or so.
            //
            // Post-data is the live stream that is stil in use.
            //
            // Returns true if the live stream is still running, false otherwise.
            Put["/Live/KeepAlive"] = p =>
            {
                var stream = this.Bind<LiveStream>();

                bool result = false;
                _staContext.Send((s) => { result = Service.KeepLiveStreamAlive(stream); }, null);
                return new
                {
                    result = result
                };
            };

            // Stop the live stream (if it is found and belongs to the recorder).
            //
            // Post-data is the live stream to stop.
            Put["/Live/Stop"] = p =>
            {
                var stream = this.Bind<LiveStream>();
                _staContext.Send((s) => { Service.StopLiveStream(stream); }, null);
                return HttpStatusCode.OK;
            };

            // Get all live streams.
            Get["/LiveStreams"] = p =>
            {
                return new
                {
                    result = Service.GetLiveStreams()
                };
            };

            // Get the live tuning state of a number of channels.
            //
            // Post-data is an object with:
            //   channels - The channels to get the live state from.
            //   stream - The live stream you want to be ignored (since it's yours), or null.
            //
            // Returns an array with all the live states for the given channels.
            Put["/ChannelsLiveState"] = p =>
            {
                var args = this.Bind<GetChannelsLiveStateArguments>();

                return new
                {
                    result = Service.GetChannelsLiveState(args.channels, args.stream)
                };
            };

            // Ask the recorder for the give live stream's tuning details (if possible).
            //
            // Post-data is the active live stream.
            //
            // Returns the service tuning details, or null if none are available.
            Put["/Live/TuningDetails"] = p =>
            {
                var stream = this.Bind<LiveStream>();

                ServiceTuning result = null;
                _staContext.Send((s) => { result = Service.GetLiveStreamTuningDetails(stream); }, null);
                return new
                {
                    result = result
                };
            };

            #endregion

            #region Teletext

            // Ask the recorder whether the given liveStream has teletext.
            //
            // Post-data is the live stream.
            //
            // Returns true if teletext is present.
            Put["/Live/HasTeletext"] = p =>
            {
                var stream = this.Bind<LiveStream>();

                bool result = false;
                _staContext.Send((s) => { result = Service.HasTeletext(stream); }, null);
                return new
                {
                    result = result
                };
            };

            // Tell the recorder to start grabbing teletext for the given live stream.
            //
            // Post-data is the live stream.
            Put["/Live/Teletext/StartGrabbing"] = p =>
            {
                var stream = this.Bind<LiveStream>();

                _staContext.Send((s) => { Service.StartGrabbingTeletext(stream); }, null);
                return HttpStatusCode.OK;
            };

            // Tell the recorder to stop grabbing teletext for the given live stream.
            //
            // Post-data is the live stream.
            Put["/Live/Teletext/StopGrabbing"] = p =>
            {
                var stream = this.Bind<LiveStream>();

                _staContext.Send((s) => { Service.StopGrabbingTeletext(stream); }, null);
                return HttpStatusCode.OK;
            };

            // Tell the recorder to start grabbing teletext for the given live stream.
            //
            // Post-data is the live stream.
            //
            // Returns true if the recorder is grabbing teletext.
            Put["/Live/Teletext/IsGrabbing"] = p =>
            {
                var stream = this.Bind<LiveStream>();

                bool result = false;
                _staContext.Send((s) => { result = Service.IsGrabbingTeletext(stream); }, null);
                return new
                {
                    result = result
                };
            };

            // Request a teletext page/subpage from the recorder for the given live stream.
            //
            // pageNumber - The teletext page number.
            // subPageNumber - The teletext subpage number.
            //
            // Post-data is the live stream.
            //
            // Returns an object with:
            //   result - The requested page content (base64-encoded), or null if the page was not ready yet.
            //   subPageCount - The total number of subpages of this page.
            Put["/Live/Teletext/GetPage/{pageNumber}/{subPageNumber}"] = p =>
            {
                var stream = this.Bind<LiveStream>();

                int subPageCount = 0;
                byte[] result = null;
                _staContext.Send((s) => { result = Service.GetTeletextPageBytes(stream, p.pageNumber, p.subPageNumber, out subPageCount); }, null);
                return new
                {
                    result = result == null ? null : Convert.ToBase64String(result, Base64FormattingOptions.None),
                    subPageCount = subPageCount
                };
            };

            #endregion
        }

        #region Arguments

        private class InitializeArguments
        {
            public string schedulerBaseUrl { get; set; }
        }

        private class AllocateCardArguments
        {
            public Channel channel { get; set; }
            public List<CardChannelAllocation> alreadyAllocated { get; set; }
            public bool useReversePriority { get; set; }
        }

        private class StartRecordingArguments
        {
            public string schedulerBaseUrl { get; set; }
            public CardChannelAllocation channelAllocation { get; set; }
            public DateTime startTimeUtc { get; set; }
            public DateTime stopTimeUtc { get; set; }
            public UpcomingProgram recordingProgram { get; set; }
            public string suggestedBaseFileName { get; set; }
        }

        private class ValidateAndUpdateRecordingArguments
        {
            public CardChannelAllocation channelAllocation { get; set; }
            public UpcomingProgram recordingProgram { get; set; }
            public DateTime stopTimeUtc { get; set; }
        }

        private class AbortRecordingArguments
        {
            public string schedulerBaseUrl { get; set; }
            public UpcomingProgram recordingProgram { get; set; }
        }

        private class TuneLiveStreamArguments
        {
            public Channel channel { get; set; }
            public CardChannelAllocation upcomingRecordingAllocation { get; set; }
            public LiveStream stream { get; set; }
        }

        private class GetChannelsLiveStateArguments
        {
            public List<Channel> channels { get; set; }
            public LiveStream stream { get; set; }
        }

        #endregion
    }
}
