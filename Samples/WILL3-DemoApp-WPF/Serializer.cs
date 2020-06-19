using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization;
using Wacom.Ink.Serialization.Model;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = System.Windows.Media.Color;

namespace Wacom
{
    public class Serializer 
    {
        #region Fields
        private Wacom.Ink.Serialization.Model.Environment mEnvironment = new Wacom.Ink.Serialization.Model.Environment();
        private readonly SensorChannel mTimestampSensorChannel;

        #endregion

        public InkModel InkDocument { get; set; } = new InkModel();

        public Serializer()
        {
            mTimestampSensorChannel = new SensorChannel(
                InkSensorType.Timestamp,
                InkSensorMetricType.Time,
                null, 0.0f, 0.0f, 0);

            mEnvironment.Properties["os.version.code"] = System.Environment.OSVersion.VersionString;
            InkDocument.InputConfiguration.Environments.Add(mEnvironment);

            InkDocument.InkTree.Root = new StrokeGroupNode(Identifier.FromNewGuid());
        }

        public void EncodeCurrentStroke(string pointerDeviceType, VectorInkBuilder inkBuilder, MediaColor brushColor)
        {
            var vectorBrush = new Wacom.Ink.Serialization.Model.VectorBrush(
                                    "will://examples/brushes/" + Guid.NewGuid().ToString(),
                                    inkBuilder.BrushApplier.Prototype.Polygons);

            var style = new Wacom.Ink.Serialization.Model.Style(vectorBrush.Name);
            style.PathPointProperties.Red   = brushColor.R / 255.0f;
            style.PathPointProperties.Green = brushColor.G / 255.0f;
            style.PathPointProperties.Blue  = brushColor.B / 255.0f;
            style.PathPointProperties.Alpha = brushColor.A / 255.0f;

            AddVectorBrushToInkDoc(pointerDeviceType, vectorBrush, style);
            EncodeCurrentStrokeCommon(pointerDeviceType, inkBuilder, style);
        }
        public void EncodeCurrentStroke(string pointerDeviceType, RasterInkBuilder inkBuilder, StrokeConstants strokeConstants, uint startRandomSeed)
        {
            var rasterBrush = inkBuilder.CreateSerializationBrush("will://examples/brushes/" + Guid.NewGuid().ToString());
            var style = new Wacom.Ink.Serialization.Model.Style(rasterBrush.Name);
            style.PathPointProperties.Red = strokeConstants.Color.R / 255.0f;
            style.PathPointProperties.Green = strokeConstants.Color.G / 255.0f;
            style.PathPointProperties.Blue = strokeConstants.Color.B / 255.0f;
            style.PathPointProperties.Alpha = strokeConstants.Color.A / 255.0f;

            AddRasterBrushToInkDoc(pointerDeviceType, rasterBrush, style, strokeConstants, startRandomSeed);   
            EncodeCurrentStrokeCommon(pointerDeviceType, inkBuilder, style);
        }

        private void EncodeCurrentStrokeCommon(string pointerDeviceType, InkBuilder inkBuilder, Style style)
        {

            // Create the ink input provider using the pointer device type
            InkInputProvider inkInputProvider = CreateAndAddInkProvider(pointerDeviceType);

            // Create the input device using EasClientDeviceInformation or any other class providing relevant info
            InputDevice inputDevice = CreateAndAddInputDevice();


            // Create the sensor context 
            SensorContext sensorContext = CreateAndAddSensorContext(inkInputProvider, inputDevice);

            // Create the input context using the environment and the sensor context
            Identifier inputContextId = CreateAndAddInputContext(sensorContext.Id);

            // Create sensor data using the input context
            SensorData sensorData = new SensorData(
                Identifier.FromNewGuid(),
                inputContextId,
                Wacom.Ink.Serialization.InkState.Plane);

            // Get the sensor data from the ink builders
            List<PointerData> pointerDataList = inkBuilder.GetPointerDataList();

            // Fill the default channels with the sensor data
            FillDefaultChannels(sensorData, sensorContext, pointerDataList);

            ///* [SAMPLE]: Add data to the custom sensor channels
            FillCustomChannels(sensorData, pointerDataList);
            //*/

            InkDocument.SensorData.Add(sensorData);

            Spline spline = inkBuilder.SplineProducer.AllData;

            PathPointLayout layout = inkBuilder.Layout;

            Stroke stroke = new Stroke(
                Identifier.FromNewGuid(),
                spline.Clone(),
                style,
                layout,
                sensorData.Id);

            StrokeNode strokeNode = new StrokeNode(Identifier.FromNewGuid(), stroke);
            InkDocument.InkTree.Root.Add(strokeNode);

        }

        private void AddRasterBrushToInkDoc(string pointerDeviceType, RasterBrush rasterBrush, Style rasterStyle, StrokeConstants strokeConstants, uint startRandomSeed)
        {
            rasterStyle.RenderModeUri = $"will3://rendering//{pointerDeviceType}";

            if (!InkDocument.Brushes.TryGetBrush(rasterBrush.Name, out Brush foundBrush))
            {
                InkDocument.Brushes.AddRasterBrush(rasterBrush);
            } 
        }

        private void AddVectorBrushToInkDoc(string pointerDeviceType, Wacom.Ink.Serialization.Model.VectorBrush vectorBrush, Style style)
        {
            style.RenderModeUri = $"will3://rendering//{pointerDeviceType}";

            if (!InkDocument.Brushes.TryGetBrush(vectorBrush.Name, out Brush foundBrush))
            {
                InkDocument.Brushes.AddVectorBrush(vectorBrush);
            }
        }

        private InkInputProvider CreateAndAddInkProvider(string pointerDeviceType)
        {
            InkInputProvider inkInputProvider = new InkInputProvider((InkInputType)Enum.Parse(typeof(InkInputType), pointerDeviceType));
            //inkInputProvider.AddProperty(); // Add properties if any
            inkInputProvider.Seal();

            Identifier inkInputProviderId = inkInputProvider.Id;
            bool res = InkDocument.InputConfiguration.InkInputProviders.Any((prov) => prov.Id == inkInputProviderId);

            if (!res)
            {
                InkDocument.InputConfiguration.InkInputProviders.Add(inkInputProvider);
            }


            return inkInputProvider;
        }

        private InputDevice CreateAndAddInputDevice()
        {
            InputDevice inputDevice = new InputDevice();
            inputDevice.Properties["dev.name"] = System.Environment.MachineName;
            //inputDevice.Properties["dev.model"] = m_eas.SystemProductName;
            //inputDevice.Properties["dev.manufacturer"] = m_eas.SystemManufacturer;
            inputDevice.Seal();

            Identifier inputDeviceId = inputDevice.Id;
            bool res = InkDocument.InputConfiguration.Devices.Any((device) => device.Id == inputDeviceId);

            if (!res)
            {
                InkDocument.InputConfiguration.Devices.Add(inputDevice);
            }

            return inputDevice;
        }

        private SensorContext CreateAndAddSensorContext(InkInputProvider inkInputProvider, InputDevice inputDevice)
        {
            // Create the sensor channel groups using the input provider and device
            SensorChannelsContext defaultSensorChannelsContext = SensorChannelsContext.CreateDefault(inkInputProvider, inputDevice);

            SensorChannelsContext specialChannelsContext = new SensorChannelsContext(
                inkInputProvider,
                inputDevice,
                new List<SensorChannel> { mTimestampSensorChannel },//, mSpecialPressureSensorChannel },
                latency: 2,
                samplingRateHint: 2);

            // Create the sensor context using the sensor channels contexts
            SensorContext sensorContext = new SensorContext();
            sensorContext.AddSensorChannelsContext(defaultSensorChannelsContext);
            //sensorContext.AddSensorChannelsContext(specialChannelsContext);

            Identifier sensorContextId = sensorContext.Id;
            bool res = InkDocument.InputConfiguration.SensorContexts.Any((context) => context.Id == sensorContextId);

            if (!res)
            {
                InkDocument.InputConfiguration.SensorContexts.Add(sensorContext);
            }

            return sensorContext;
        }

        private Identifier CreateAndAddInputContext(Identifier sensorContextId)
        {
            InputContext inputContext = new InputContext(mEnvironment.Id, sensorContextId);

            Identifier inputContextId = inputContext.Id;
            bool res = InkDocument.InputConfiguration.InputContexts.Any((context) => context.Id == inputContextId);

            if (!res)
            {
                InkDocument.InputConfiguration.InputContexts.Add(inputContext);
            }

            return inputContextId;
        }

        private void FillDefaultChannels(SensorData sensorData, SensorContext sensorContext, List<PointerData> pointerDataList)
        {
            SensorChannelsContext channels = sensorContext.DefaultSensorChannelsContext;

            sensorData.AddData(channels.GetChannel(InkSensorType.X), pointerDataList.Select((pd) => pd.X).ToList());
            sensorData.AddData(channels.GetChannel(InkSensorType.Y), pointerDataList.Select((pd) => pd.Y).ToList());
            sensorData.AddTimestampData(channels.GetChannel(InkSensorType.Timestamp), pointerDataList.Select((pd) => pd.Timestamp).ToList());

            if (pointerDataList[0].Force.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Pressure), pointerDataList.Select((pd) => pd.Force.Value).ToList());
            }

            if (pointerDataList[0].Radius.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.RadiusX), pointerDataList.Select((pd) => pd.Radius.Value).ToList());
            }

            if (pointerDataList[0].AzimuthAngle.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Azimuth), pointerDataList.Select((pd) => pd.AzimuthAngle.Value).ToList());
            }

            if (pointerDataList[0].AltitudeAngle.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Altitude), pointerDataList.Select((pd) => pd.AltitudeAngle.Value).ToList());
            }
        }

        private void FillCustomChannels(SensorData sensorData, List<PointerData> pointerDataList)
        {
            /*			ChannelData timestampChannelData = new ChannelData(m_timestampSensorChannel);
                        ChannelData specialPressureChannelData = new ChannelData(m_specialPressureSensorChannel);

                        int timestampChannelIndex = sensorData.RegisterCustomChannel(timestampChannelData);
                        int specialPressureChannelIndex = sensorData.RegisterCustomChannel(specialPressureChannelData);

                        // Offset the timestamp values
                        List<long> timestampValues = pointerDataList.Select(pd => pd.Timestamp + 50).ToList();
                        sensorData.AddToCustomChannel(timestampChannelIndex, timestampValues);

                        Random rand = new Random();
                        // Generate a random pressure value for each pointer data
                        List<float> specialPressureValues = pointerDataList.Select(pd => (float)rand.Next(6, 12)).ToList();
                        sensorData.AddToCustomChannel(specialPressureChannelIndex, specialPressureValues);*/
        }

    
    }
}
