﻿using Pk3DSRNGTool.Core;

namespace Pk3DSRNGTool
{
    public class Result7 : RNGResult
    {
        public override GameVersion Version { get; protected set; } = GameVersion.Gen7;
        public ulong RandNum;
        public int FrameDelayUsed;

        public Result7()
        {
            RandNum = RNGPool.getcurrent64;
            FrameDelayUsed = RNGPool.index;
        }
    }
}
