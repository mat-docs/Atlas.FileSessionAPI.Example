using MAT.AtlasSessionApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MAT.FileSessionApi.Example
{
    internal static class Program
    {
        private static void Main()
        {

            const string menu = @"
            #####################################################################################################
            
            Example code showing how to read-write ATLAS session files (Verify settings in Main() before executing)

            Option 1 - Create a new session file with bespoke parameters
            Option 2 - Read all parameters from an existing session file (with optional associated sessions)
            Option 3 - Read selected parameters from an existing session file (with optional associated sessions)
            Option 4 - Create a new session file with bespoke parameters and then read it
            
            #####################################################################################################";

            // ################ Settings for Options 1 & 4 ################

            // The complete path for the session
            const string fullPathToNewSsn = @"C:\temp\dummy.ssn";
            
            // ################ Settings for Options 2 & 3 ################

            // Full file path to the SSN to read
            const string fullPathToExistingSsn = @"m:\SessionBrowser\SSN\VTS - Copy\Event Compare 180503163009.ssn";
            // File names for any associated sessions
            string[] associatedSessionFileNames =
            {
                "Event Compare 180503163009.VTS.001.ssv",
                "Event Compare 180503163009.VTS.002.ssv",
                "Event Compare 180503163009.VTS.003.ssv",
                "Event Compare 180503163009.VTS.004.ssv",
                "Event Compare 180503163009.VTS.005.ssv",
                "Event Compare 180503163009.VTS.006.ssv"
            };


            
            // Let the use enter an option.
            Console.SetWindowSize(200, 60);
            Console.WriteLine(menu);

            // Read in the user option.
            Console.WriteLine();
            Console.WriteLine("Please enter a number to select one of the above options:");
            var option = Console.ReadLine();
            var optionNumber = 0;
            if (option != null)
            { 
                int.TryParse(option, out optionNumber);
            }

            // process the option
            switch (optionNumber)
            {
                case 1:
                    CreateNewSession(fullPathToNewSsn, false);
                    break;
                case 2:
                    ReadExistingSession(fullPathToExistingSsn, false, associatedSessionFileNames, true);
                    break;
                case 3:
                    ReadExistingSession(fullPathToExistingSsn, false, associatedSessionFileNames, false);
                    break;
                case 4:
                    CreateNewSession(fullPathToNewSsn, false);
                    ReadExistingSession(fullPathToNewSsn);
                    break;
                default:
                    Console.WriteLine("Entered option was not correct {0}, exiting application.", option);
                    break;
            }

            Console.ReadLine();
        }

        // called to create a new session file
        private static void CreateNewSession(string testFileNameSsn, bool addDataWithPeriodicValues)
        {
            try
            {
                // set some session vars
                const long sessionStartTime = 32400000000000L; // the time at which the recording starts 09:00:0.000
                const long sessionEndTime = 33000000000000L; // the time at which the recording ends 09:10:0.000
                const long sessionLapInterval = 60000000000L; // time interval for lap generation 00:01:0.000
                const int sessionParameterCount = 10; // the number of session parameters we will create
                const long sessionSampleInterval = 100000000L; // time interval for each sample 100ms.
                const double parameterValueMultiplier = 100.0; // multiplier used to create max (and min) range of the example parameters

                // create a new instance of the file session writer
                using (ISessionWriter sessionWriter = new FileSessionWriter(testFileNameSsn))
                {
                    // add some Session Details
                    sessionWriter.AddSessionDetails("Driver", "A. Person");
                    sessionWriter.AddSessionDetails("Car", "F1 MP V12");
                    sessionWriter.AddSessionDetails("ECU Version", "T310Bios B122, chassis C142, engine E144, KcuBios B20F, eKERS 9332, Cbt610 6111, fiaapp F134");
                    sessionWriter.AddSessionDetails("Unit Data Source", "DataLab/Burst");

                    // create the parameter groups and associated sub groups
                    const string parameterGroup = "TestGroup";
                    var parameterSubGroups = new List<string>
                    {
                        "SubGroup1",
                        "SubGroup2",
                        "SubGroup3",
                        "SubGroup4",
                        "SubGroup5"
                    };

                    // create parameters and add them to the session
                    for (uint parameterIndex = 0; parameterIndex < sessionParameterCount; parameterIndex++)
                    {
                        // create the parameter name and description
                        string name = $"Parameter{parameterIndex + 1}";
                        string description = $"Test Parameter {parameterIndex + 1}";

                        if (parameterIndex < 5)
                        {
                            string parameterUnit = $"U{parameterIndex + 1}";
                            var physicalRange = new Range(-100, 100.0);
                            sessionWriter.BuildRationalParameter(parameterGroup, name, physicalRange)
                                .Description(description)
                                .SubGroups(parameterSubGroups)
                                .Units(parameterUnit)
                                .OnPeriodicChannel(Frequency.Interval(sessionSampleInterval))
                                .AddToSession();
                        }
                        else
                        {
                            if (parameterIndex < 8)
                            {
                                sessionWriter.BuildTextParameter(parameterGroup, name)
                                    .AddLookup(0, "NO")
                                    .AddLookup(1.0, "YES")
                                    .DefaultValue("NO")
                                    .SubGroups(parameterSubGroups)
                                    .Description(description)
                                    .OnPeriodicChannel(Frequency.Interval(sessionSampleInterval), DataType.Unsigned8Bit)
                                    .AddToSession();
                            }
                            else
                            {
                                TextParameterBuilder textParameter = sessionWriter.BuildTextParameter(parameterGroup, name);
                                textParameter.AddLookup(1.0, "One")
                                    .AddLookup(2.0, "Two")
                                    .AddLookup(3.0, "Three")
                                    .AddLookup(4.0, "Four")
                                    .DefaultValue("One");

                                // PhysicalRange will automatically calculated by lookup table. Below statement is just for an example.
                                textParameter.PhysicalRange(new Range(1, 4));

                                textParameter.SubGroups(parameterSubGroups).Description(description);
                                textParameter.OnPeriodicChannel(Frequency.Interval(sessionSampleInterval), DataType.Signed8Bit).AddToSession();
                            }
                        }
                    }

                    // the parameters have been created so they need to be committed before they can be written to 
                    sessionWriter.CommitParameters();

                    // calculate the number of samples we will write to the session
                    var numberOfSamples = Convert.ToInt32((sessionEndTime - sessionStartTime) / sessionSampleInterval) + 1;

                    // create the arrays that contain the data that we will write to the session
                    var dataTimeStamps = new long[numberOfSamples];
                    var dataSineValues = new float[numberOfSamples];
                    var dataOnOffValues = new byte[numberOfSamples];
                    var dataOneToFourValues = new byte[numberOfSamples];

                    // set the initial timestamp value
                    var currentTimeStamp = sessionStartTime;
                    dataTimeStamps[0] = sessionStartTime;
                    dataSineValues[0] = (float)(parameterValueMultiplier * Math.Sin(0));

                    // create some random data for the parameter channels
                    var random = new Random();
                    for (int sampleIndex = 0; sampleIndex < dataTimeStamps.Length; sampleIndex++)
                    {
                        // populate the data arrays
                        dataTimeStamps[sampleIndex] = currentTimeStamp;
                        dataSineValues[sampleIndex] = (float)(parameterValueMultiplier * Math.Sin(sampleIndex * Math.PI / 360));
                        dataOnOffValues[sampleIndex] = (byte)random.Next(0, 2);
                        dataOneToFourValues[sampleIndex] = (byte)random.Next(0, 5);

                        // increment the sample timestamp
                        currentTimeStamp += sessionSampleInterval;
                    }

                    // populate the parameter channels with the data values
                    var channel = sessionWriter.Channels.GetEnumerator();
                    for (var channelIndex = 0; channelIndex < sessionWriter.Channels.Count; channelIndex++)
                    {
                        channel.MoveNext();

                        byte[] byteData;
                        if (channelIndex < 5)
                        {
                            if (addDataWithPeriodicValues)
                            {
                                sessionWriter.WritePeriodicValues(channel.Current.Value, dataTimeStamps[0], dataSineValues, dataSineValues.Length);
                                continue;
                            }

                            byteData = new byte[numberOfSamples * 4];
                            Buffer.BlockCopy(dataSineValues, 0, byteData, 0, byteData.Length);
                        }
                        else
                        {
                            byteData = new byte[numberOfSamples];
                            if (channelIndex < 8)
                            {
                                Buffer.BlockCopy(dataOnOffValues, 0, byteData, 0, dataOnOffValues.Length);
                            }
                            else
                            {
                                Buffer.BlockCopy(dataOneToFourValues, 0, byteData, 0, dataOneToFourValues.Length);
                            }
                        }

                        if (addDataWithPeriodicValues)
                        {
                            sessionWriter.WritePeriodicValues(channel.Current.Value, dataTimeStamps[0], dataSineValues, dataSineValues.Length);
                        }
                        else
                        {
                            sessionWriter.WritePeriodicData(channel.Current.Value, dataTimeStamps[0], numberOfSamples, byteData);
                        }
                    }

                    // add some laps to the session
                    long timestamp = sessionStartTime + sessionLapInterval;
                    int lapNumber = 1;
                    while (timestamp < sessionEndTime)
                    {
                        var lapType = LapType.Default;
                        if (lapNumber == 1)
                        {
                            lapType = LapType.OutLap;
                        }
                        else if (timestamp + sessionLapInterval >= sessionEndTime)
                        {
                            lapType = LapType.InLap;
                        }

                        sessionWriter.AddLap(lapNumber, timestamp, lapType);
                        lapNumber++;
                        timestamp += sessionLapInterval;
                    }

                    // close the session
                    sessionWriter.CloseSession();

                    // we are done!
                    Console.WriteLine("Session Created");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        // called to read an existing session file
        private static void ReadExistingSession(string testFileNameSsn, bool loadLatestVersionOfAssociatedSessions = false, string[] associatedSessionFileNames = null, bool readAllParametersData = true)
        {
            try
            {
                ISessionReader sessionReader;

                if (loadLatestVersionOfAssociatedSessions)
                {
                    sessionReader = new FileSessionReader(testFileNameSsn, loadLatestVersionOfAssociatedSessions);
                }
                else if (associatedSessionFileNames != null)
                {
                    sessionReader = new FileSessionReader(testFileNameSsn, associatedSessionFileNames);
                }
                else
                {
                    sessionReader = new FileSessionReader(testFileNameSsn);
                }

                using (sessionReader)
                {
                    if (!readAllParametersData)
                    {
                        var paramIds = new[]
                        {
                            "fToneFrequencyBrakeControl:BrakeControl",
                            "tTAG320TotalDistanceTimestampFIA:TAG320BIOS",
                            "tTAG320APBoardBottomMaxTimestampFIA:TAG320BIOS",
                            "nEngine:FIA",
                            "vCar:chassis",
                            "vCar_vTAG:APP9",
                            "nEngine_vTAG:APP9",
                            "sLap:chassis",
                            "vCar:Chassis",
                            "sLap:Chassis",
                            "NEngineStatus:engine"
                        };

                        foreach (var paramId in paramIds)
                        {
                            Parameter parameter;
                            if (sessionReader.ParametersByIdentifier.TryGetValue(paramId, out parameter))
                            {
                                // Read the data for the merged parameter with the frequency.
                                long sampleRate = parameter.Channels.Select(channel => channel.Value.Interval).Concat(new long[] { 0 }).Max();
                                if (sampleRate > 0)
                                {
                                    DataPoint[] bufferForMergedParams = sessionReader.GetData(parameter.Identifier,
                                        sessionReader.StartTime,
                                        sessionReader.EndTime,
                                        Frequency.Interval(sampleRate),
                                        true);

                                    Console.WriteLine("Merged Parameter: {0} \t\t Data Count: {1}", parameter.Identifier, bufferForMergedParams.Length);
                                }

                                foreach (var channel in parameter.Channels)
                                {
                                    DataPoint[] buffer = sessionReader.GetSamples(parameter.Identifier, channel.Value.Id,
                                        sessionReader.StartTime, sessionReader.EndTime);

                                    Console.WriteLine("Parameter: {0} \t\t Channel ID: {1} \t\t Channel Type: {2} \t\t Data Count: {3}",
                                        parameter.Identifier, channel.Value.Id, channel.Value.DataSource, buffer.Length);
                                }

                                // Read the data for the primary channel with 100.0 Hz frequency.
                                double frequency = 100.0;

                                // Calculate expected number of samples between start and end time at the given frequency.
                                long sampleInterval = Frequency.Hz(frequency).Interval();
                                long expectedSamples = (long)Math.Floor((sessionReader.EndTime - sessionReader.StartTime) / (double)sampleInterval) + 1;

                                DataPoint[] bufferForPrimaryChannel = sessionReader.GetData(parameter.Identifier,
                                                                                    parameter.PrimaryChannel.Id,
                                                                                    sessionReader.StartTime,
                                                                                    sessionReader.EndTime,
                                                                                    Frequency.Hz(frequency),
                                                                                    true);

                                Console.WriteLine("Primary Parameter @ 100.0 Hz: {0} \t\t Channel ID: {1} \t\t Channel Type: {2} \t\t Data Count: {3}",
                                        parameter.Identifier, parameter.PrimaryChannel.Id, parameter.PrimaryChannel.DataSource, bufferForPrimaryChannel.Length);

                                if (bufferForPrimaryChannel.Length != expectedSamples)
                                {
                                    Console.WriteLine("******** FAILED Expected: {0} \t\t Sample Count: {1} ********", expectedSamples, bufferForPrimaryChannel.Length);
                                }
                            }
                        }

                        var virtualParamIds = new[]
                        {
                            "BDashLight01",
                            "gVertF",
                            "TWasteGateL",
                            "BDriverDefault01ActiveDesc",
                            "TGearboxCaseL",
                            "NFBibLifeCode"
                        };

                        foreach (var paramId in virtualParamIds)
                        {
                            const int lapNumToGetData = 1;
                            VirtualParameter parameter;
                            if (sessionReader.VirtualParametersByIdentifier.TryGetValue(paramId, out parameter))
                            {
                                IEnumerator<Lap> laps = sessionReader.Laps.GetEnumerator();
                                for (int i = 0; i < lapNumToGetData; i++)
                                {
                                    laps.MoveNext();
                                }

                                DataPoint[] buffer = sessionReader.GetSamples(parameter.Identifier, laps.Current.StartTime, laps.Current.EndTime);
                                Console.WriteLine("Virtual Parameter: {0} \t\t Data Count: {1}", parameter.Identifier, buffer.Length);
                            }
                        }
                    }
                    else
                    {
                        // read each of the laps in the session
                        Console.WriteLine(Environment.NewLine + "Session Lap Collection" + Environment.NewLine);
                        var sessionLaps = sessionReader.Laps;
                        foreach (var lap in sessionLaps)
                        {
                            Console.WriteLine("  Lap {0}\t {1}\t {2}\t {3}", lap.Number, lap.Type, lap.StartTime,
                                lap.EndTime);
                        }

                        Console.WriteLine(Environment.NewLine + "   Session Details" + Environment.NewLine);
                        foreach (var detail in sessionReader.SessionItems)
                        {
                            Console.WriteLine("      {0}\t\t{1}", detail.Key, detail.Value);
                        }

                        // read each of the parameters in the session
                        Console.WriteLine(Environment.NewLine + "Session Parameter Collection" + Environment.NewLine);
                        foreach (var parameter in sessionReader.Parameters)
                        {
                            // debug the parameter information
                            Console.WriteLine("  Parameter Identifier: \t\t{0}", parameter.Identifier);
                            Console.WriteLine("     Conversion Identifier: \t\t{0}", parameter.Conversion.Identifier);
                            Console.WriteLine("     Channel Counts: \t\t\t{0}", parameter.Channels.Count);

                            // debug the associated channel information for the parameter
                            foreach (var channel in parameter.Channels)
                            {
                                Console.WriteLine("         Parameter Channel Key: \t{0}", channel.Key);
                                Console.WriteLine("         Parameter Channel Value: \t{0}", channel.Value.Id);
                                Console.WriteLine("         Parameter Channel DataType: \t{0}", channel.Value.DataType);
                                Console.WriteLine("         Parameter Channel Frequency: \t{0}", channel.Value.Frequency);
                            }

                            Console.WriteLine(Environment.NewLine);
                        }

                        // each of the channels can also be read directly
                        Console.WriteLine(Environment.NewLine + "Session Channel Collection" + Environment.NewLine);
                        foreach (var channel in sessionReader.Channels)
                        {
                            Console.WriteLine("  Channel Key: {0} Value: {1}", channel.Key, channel.Value.Id);
                        }

                        // debug each of the conversions
                        Console.WriteLine(Environment.NewLine + "Session Conversion Collection" + Environment.NewLine);
                        foreach (var conversion in sessionReader.Conversions)
                        {
                            Console.WriteLine("     Conversion Key: \t\t\t{0}\t{1}", conversion.Key, conversion.Value);
                            Console.WriteLine("     Conversion Value Identifier: \t{0}", conversion.Value.Identifier);
                            Console.WriteLine("     Conversion Value Format: \t\t{0}", conversion.Value.Format);
                            Console.WriteLine("     Conversion Value Units: \t\t{0}", conversion.Value.Units);

                            Console.WriteLine(Environment.NewLine);
                        }

                        // get the non interpolated sample data for each parameter for the whole session
                        Console.WriteLine(Environment.NewLine + "Session Data Samples - Non Interpolated" + Environment.NewLine);
                        foreach (var parameter in sessionReader.Parameters)
                        {
                            // get the sample data for the whole session, we do want interpolated results.
                            DataPoint[] buffer = sessionReader.GetSamples(parameter.Identifier, sessionReader.StartTime, sessionReader.EndTime);

                            Console.WriteLine("  Parameter: \t\t{0}", parameter.Identifier);
                            Console.WriteLine("    Sample Count: \t{0}", buffer.Length);

                            foreach (var channel in parameter.Channels)
                            {
                                DataPoint[] channelBuffer = sessionReader.GetSamples(parameter.Identifier, channel.Value.Id,
                                    sessionReader.StartTime, sessionReader.EndTime);

                                Console.WriteLine("     Channel Id: \t{0}", channel.Value.Id);
                                Console.WriteLine("     Sample Count: \t{0}", channelBuffer.Length);
                            }

                            Console.WriteLine(Environment.NewLine);
                        }

                        // get the interpolated data for each parameter at a fixed frequency
                        Console.WriteLine(Environment.NewLine + "Session Data Samples - Interpolated @ 1000hz" +
                                          Environment.NewLine);
                        foreach (var parameter in sessionReader.Parameters)
                        {
                            // get the sample data for the whole session, we do not want interpolated results
                            DataPoint[] buffer = sessionReader.GetSamples(parameter.Identifier, sessionReader.StartTime, sessionReader.EndTime);

                            Console.WriteLine("  Parameter: \t\t{0}", parameter.Identifier);
                            Console.WriteLine("    Sample Count: \t{0}", buffer.Length);

                            foreach (var channel in parameter.Channels)
                            {
                                DataPoint[] channelBuffer = sessionReader.GetData(parameter.Identifier, channel.Value.Id,
                                    sessionReader.StartTime, sessionReader.EndTime, Frequency.KHz(1.0), true);

                                Console.WriteLine("     Channel Id: \t{0}", channel.Value.Id);
                                Console.WriteLine("     Sample Count: \t{0}", channelBuffer.Length);

                                // Calculate expected number of samples between start and end time at the given frequency.
                                long sampleInterval = Frequency.Hz(1000.0).Interval();
                                long expectedSamples = (long)Math.Floor((sessionReader.EndTime - sessionReader.StartTime) / (double)sampleInterval) + 1;
                                Debug.Assert(channelBuffer.Length != expectedSamples);

                                if (channelBuffer.Length != expectedSamples)
                                {
                                    Console.WriteLine("******** FAILED Expected: {0} \t\t Sample Count: {1} ********", expectedSamples, channelBuffer.Length);
                                }
                            }

                            Console.WriteLine(Environment.NewLine);
                        }

                        // get the data for virtual parameters.
                        Console.WriteLine(Environment.NewLine + "Data for Virtual Parameters..." + Environment.NewLine);
                        foreach (var parameter in sessionReader.VirtualParameters)
                        {
                            // get the sample data for the whole session, we do not want interpolated results
                            DataPoint[] buffer = sessionReader.GetSamples(parameter.Identifier, sessionReader.StartTime, sessionReader.EndTime);

                            Console.WriteLine("  Virtual Parameter: \t\t{0}", parameter.Identifier);
                            Console.WriteLine("    Sample Count: \t{0}", buffer.Length);

                            Console.WriteLine(Environment.NewLine);
                        }
                    }

                    // Update the session detail for driver which will create .sse file.
                    string detailName = "Driver";
                    string detailValue = "F1 Driver";
                    sessionReader.SessionDetail.Update(detailName, detailValue);

                    // Update multiple session details into .sse file.
                    string[] detailNames = { "NewTag", "Circuit" };
                    string[] detailValues = { "New Car", "Planet-M" };
                    sessionReader.SessionDetail.Update(detailNames, detailValues);

                    // close the session, object will be disposed here. CloseSession() will automatically called while disposing the object also.
                    // It's to demonstrate dispose can be called multiple times without any issue.
                    // If session details are changed then it'll save new session details in .sse file while closing the session.
                    sessionReader.CloseSession();

                    // we are done!
                    Console.WriteLine(Environment.NewLine + "Session processing complete");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }
    }
}