﻿namespace oni
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;
     
    using lib;


    // Make managed version of oni_frame_t
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct frame_t
    {
        public ulong clock; // Base clock counter
        public ushort num_dev; // Number of devices in frame
        public byte corrupt; // Is this frame corrupt?
        public uint* dev_idxs; // Array of device indices in frame
        public uint* dev_offs; // Device data offsets within data block
        public byte* data; // Multi-device raw data block
        public uint data_sz; // Size in bytes of data buffer
    }

    public unsafe class Frame : SafeHandleZeroOrMinusOneIsInvalid
    {
        //[DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        //static extern void CopyMemory(IntPtr Destination, IntPtr Source, uint Length);

        public const int MaxDevPerFrame = 32; // Copy from oni.h

        protected Frame() 
        : base(true)
        {
        }

        internal void Map(Dictionary<int, device_t> dev_map)
        {
            DeviceMap = dev_map;
            var frame = (frame_t*)handle.ToPointer();

            DeviceIndices = new List<int>(frame->num_dev);
            for (int i = 0; i < frame->num_dev; i++)
            {
                DeviceIndices.Add((int)*(frame->dev_idxs + i));
            }
        }

        public ulong Clock()
        {
            return ((frame_t*)handle.ToPointer())->clock;
        }

        public bool Corrupt()
        {
            return ((frame_t*)handle.ToPointer())->corrupt != 0;
        }

        // Ideally, I would like this to be a "Span" into the exsting, allocated frame
        // Now, there are _two_ deep copies happening here as far as I can tell which is ridiculous
        public T[] Data<T>(int dev_idx) where T : struct
        {
            var frame = (frame_t*)handle.ToPointer();

            // Device position in frame
            var pos = DeviceIndices.FindIndex(x => x == dev_idx);

            // If device is not in frame
            if (pos == -1)
            {
                throw new ONIException((int)Error.DEVIDX);
            }

            // Get the read size and offset for this device
            var num_bytes = DeviceMap[dev_idx].read_size;
            var byte_offset = frame->dev_offs[pos];

            var buffer = new byte[num_bytes];
            var output = new T[num_bytes / Marshal.SizeOf(default(T))];
            var start_ptr = frame->data + byte_offset;

            // TODO: Seems like we should be able to copy directly into output!
            Marshal.Copy((IntPtr)start_ptr, buffer, 0, (int)num_bytes);
            Buffer.BlockCopy(buffer, 0, output, 0, (int)num_bytes);
            return output;
        }

        // Ideally, I would like this to be a "Span" into the exsting, allocated frame
        // Now, there are _two_ deep copies happening here as far as I can tell which is ridiculous
        public T[] Data<T>(int dev_idx, int num_bytes) where T : struct
        {
            var frame = (frame_t*)handle.ToPointer();

            // Device position in frame
            var pos = DeviceIndices.FindIndex(x => x == dev_idx);

            // If device is not in frame
            if (pos == -1)
            {
                throw new ONIException((int)Error.DEVIDX);
            }

            // Get the read size and offset for this device
            var byte_offset = frame->dev_offs[pos];

            var buffer = new byte[num_bytes];
            var output = new T[num_bytes / Marshal.SizeOf(default(T))];
            var start_ptr = frame->data + byte_offset;

            // TODO: Seems like we should be able to copy directly into output!
            Marshal.Copy((IntPtr)start_ptr, buffer, 0, (int)num_bytes);
            Buffer.BlockCopy(buffer, 0, output, 0, (int)num_bytes);
            return output;
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.oni_destroy_frame(handle);
            return true;
        }

        // Devices with data in this frame
        public List<int> DeviceIndices { get; private set; }

        // Global device index -> device_t struct
        private Dictionary<int, device_t> DeviceMap;
    }
}
