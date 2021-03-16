﻿#region Copyright
// --------------------------------------------------------------------------------------------------------------------
// MIT License
// Copyright(c) 2020 Greg Cannon

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// --------------------------------------------------------------------------------------------------------------------
#endregion Copyright

#region Usings

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using Waveshare.Common;

#endregion Usings

namespace Waveshare.Devices.Epd7in5_V2
{
    /// <summary>
    /// Type: Waveshare 7.5inch e-Paper V2
    /// Color: Black and White
    /// Display Resolution: 800*480
    /// </summary>
    // ReSharper disable once InconsistentNaming
    internal sealed class Epd7In5_V2 : EPaperDisplayBase
    {

        //########################################################################################

        #region Properties

        /// <summary>
        /// Pixel Width of the Display
        /// </summary>
        public override int Width { get; } = 800;

        /// <summary>
        /// Pixel Height of the Display
        /// </summary>
        public override int Height { get; } = 480;

        /// <summary>
        /// Get Status Command
        /// </summary>
        protected override byte GetStatusCommand { get; } = (byte)Epd7In5_V2Commands.GetStatus;

        /// <summary>
        /// Start Data Transmission Command
        /// </summary>
        protected override byte StartDataTransmissionCommand { get; } = (byte)Epd7In5_V2Commands.DataStartTransmission2;

        /// <summary>
        /// Stop Data Transmission Command
        /// </summary>
        protected override byte StopDataTransmissionCommand { get; } = (byte)Epd7In5_V2Commands.DataStop;

        #endregion Properties

        //########################################################################################

        #region Public Methods

        /// <summary>
        /// Clear the Display to White
        /// </summary>
        public override void Clear()
        {
            FillColor(Epd7In5_V2Commands.DataStartTransmission1, Color.White);
            FillColor(Epd7In5_V2Commands.DataStartTransmission2, Color.White);
            TurnOnDisplay();
        }

        /// <summary>
        /// Clear the Display to Black
        /// </summary>
        public override void ClearBlack()
        {
            FillColor(Epd7In5_V2Commands.DataStartTransmission2, Color.Black);
            TurnOnDisplay();
        }

        /// <summary>
        /// Power the controller on.  Do not use with SleepMode.
        /// </summary>
        public override void PowerOn()
        {
            SendCommand(Epd7In5_V2Commands.PowerOn);
            DeviceWaitUntilReady();
        }

        /// <summary>
        /// Power the controler off.  Do not use with SleepMode.
        /// </summary>
        public override void PowerOff()
        {
            SendCommand(Epd7In5_V2Commands.PowerOff);
            DeviceWaitUntilReady();
        }

        /// <summary>
        /// Send the Display into SleepMode
        /// </summary>
        public override void Sleep()
        {
            PowerOff();
            SendCommand(Epd7In5_V2Commands.DeepSleep);
            SendData(0xA5);
        }

        /// <summary>
        /// Wait until the display is ready
        /// </summary>
        public void DeviceWaitUntilReady()
        {
            WaitUntilReady();
            Thread.Sleep(200);
        }

        #endregion Public Methods

        //########################################################################################

        #region Protected Methods

        /// <summary>
        /// Device specific Initializer
        /// </summary>
        protected override void DeviceInitialize()
        {
            Reset();

            SendCommand(Epd7In5_V2Commands.BoosterSoftStart);
            SendData(0x17);
            SendData(0x17);
            SendData(0x27);
            SendData(0x17);

            SendCommand(Epd7In5_V2Commands.PowerSetting);
            SendData(0x07); // VGH: 20V
            SendData(0x17); // VGL: -20V
            SendData(0x3f); // VDH: 15V
            SendData(0x3f); // VDL: -15V

            SendCommand(Epd7In5_V2Commands.PowerOn);
            Thread.Sleep(100);
            DeviceWaitUntilReady();

            SendCommand(Epd7In5_V2Commands.PanelSetting);
            SendData(0x1F); // KW-3f   KWR-2F	BWROTP 0f	BWOTP 1f

            SendCommand(Epd7In5_V2Commands.TconResolution);
            SendData(0x03); // source 800
            SendData(0x20);
            SendData(0x01); // gate 480
            SendData(0xe0);

            SendCommand(Epd7In5_V2Commands.DualSpi);
            SendData(0x00);

            SendCommand(Epd7In5_V2Commands.VcomAndDataIntervalSetting);
            SendData(0x10);
            SendData(0x07);

            SendCommand(Epd7In5_V2Commands.TconSetting);
            SendData(0x22);
        }

        /// <summary>
        /// Turn the Display PowerOn after a Sleep
        /// </summary>
        protected override void TurnOnDisplay()
        {
            SendCommand(Epd7In5_V2Commands.DisplayRefresh);
            Thread.Sleep(100);
            DeviceWaitUntilReady();
        }

        /// <summary>
        /// Convert a pixel to a DataByte
        /// </summary>
        /// <param name="r">Red color byte</param>
        /// <param name="g">Green color byte</param>
        /// <param name="b">Blue color byte</param>
        /// <returns>Pixel converted to specific byte value for the hardware</returns>
        protected override byte ColorToByte(byte r, byte g, byte b)
        {
            return (byte)(255 - (r * 0.299 + g * 0.587 + b * 0.114));
        }

        #endregion Protected Methods

        //########################################################################################

        #region Internal Methods

        /// <summary>
        /// Convert a Bitmap to a Byte Array
        /// </summary>
        /// <param name="image">Bitmap image to convert</param>
        /// <returns>Byte array of image</returns>
        internal override byte[] BitmapToData(Bitmap image)
        {
            const int pixelPerByte = 8;
            int maxX = Math.Min(Width, image.Width);
            int maxY = Math.Min(Height, image.Height);
            sbyte[,] data = new sbyte[maxX, maxY];

            // Convert to greyscale
            BitmapData inputData = image.LockBits(new Rectangle(0, 0, maxX, maxY), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                IntPtr scanLine = inputData.Scan0;
                byte[] line = new byte[inputData.Stride];
                int xpos;
                for (int y = 0; y < inputData.Height; y++, scanLine += inputData.Stride)
                {
                    Marshal.Copy(scanLine, line, 0, line.Length);
                    for (int x = 0; x < maxX; x++)
                    {
                        xpos = x * 3;
                        data[x, y] = (sbyte)(64d * (ColorToByte(line[xpos + 2], line[xpos + 1], line[xpos + 0]) / 255d - 0.5d));
                    }
                }
            }
            finally
            {
                image.UnlockBits(inputData);
            }

            // Dither to monochrome 8 pixels per byte using Floyd-Steinberg
            byte[] masks = new byte[] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };
            int outputStride = Width / pixelPerByte;
            byte[] output = new byte[outputStride * Height];
            for (int y = 0; y < maxY; y++)
            {
                byte[] line = new byte[outputStride];
                for (int x = 0; x < maxX; x++)
                {
                    bool j = data[x, y] > 0;
                    if (j)
                        line[x / 8] |= masks[x % 8];
                    sbyte error = (sbyte)(data[x, y] - (j ? 32 : -32));
                    if (x < maxX - 1)
                        data[x + 1, y] += (sbyte)(7 * error / 16);
                    if (y < maxY - 1)
                    {
                        if (x > 0)
                            data[x - 1, y + 1] += (sbyte)(3 * error / 16);
                        data[x, y + 1] += (sbyte)(5 * error / 16);
                        if (x < maxX - 1)
                            data[x + 1, y + 1] += (sbyte)(1 * error / 16);
                    }
                }
                Buffer.BlockCopy(line, 0, output, y * outputStride, outputStride);
            }
            return output;
        }

        /// <summary>
        /// Merge eight DataBytes into one Byte
        /// </summary>
        /// <param name="pixel1"></param>
        /// <param name="pixel2"></param>
        /// <param name="pixel3"></param>
        /// <param name="pixel4"></param>
        /// <param name="pixel5"></param>
        /// <param name="pixel6"></param>
        /// <param name="pixel7"></param>
        /// <param name="pixel8"></param>
        /// <returns></returns>
        internal static byte MergePixelDataInByte(byte pixel1, byte pixel2, byte pixel3, byte pixel4, byte pixel5, byte pixel6, byte pixel7, byte pixel8)
        {
            var output = (byte)((pixel1 << 7) | (pixel2 << 6) | (pixel3 << 5) | (pixel4 << 4) | (pixel5 << 3) | (pixel6 << 2) | (pixel7 << 1) | pixel8);
            return output;
        }

        #endregion

        //########################################################################################

        #region Private Methods

        /// <summary>
        /// Helper to send a Command based o the Epd7In5_V2Commands Enum
        /// </summary>
        /// <param name="command">Command to send</param>
        private void SendCommand(Epd7In5_V2Commands command)
        {
            SendCommand((byte)command);
        }

        /// <summary>
        /// Fill the screen with a color
        /// </summary>
        /// <param name="command">Start Data Transmission Command</param>
        /// <param name="color">Color to fill the screen</param>
        private void FillColor(Epd7In5_V2Commands command, Color color)
        {
            const int pixelPerByte = 8;
            var displayBytes = Width / pixelPerByte * Height;

            var pixelData = ColorToByte(color.R, color.G, color.B);
            var eightColorPixel = MergePixelDataInByte(pixelData, pixelData, pixelData, pixelData, pixelData, pixelData, pixelData, pixelData);

            SendCommand(command);
            for (var i = 0; i < displayBytes; i++)
            {
                SendData(eightColorPixel);
            }
        }

        #endregion Private Methods

        //########################################################################################

    }
}