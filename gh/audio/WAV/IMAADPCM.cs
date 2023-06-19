﻿/**********************************************************************\

 iv_audio_rip Copyright (C) 2009  DerPlaya78
 
 Modified and adapted for RageLib from iv_audio_rip

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.

\**********************************************************************/

/**********************************************************************\

Thanks to https://github.com/Flitskikker/IMAADPCMEncoder
And evanmurray

\**********************************************************************/

namespace WAV
{
    public class IMAADPCM
    {
        public struct ADPCMState
        {
            public short valprev;
            public byte index;
            public int sample;
        }

        private static readonly int[] indexTable = {
                                                       -1, -1, -1, -1, 2, 4, 6, 8,
                                                       -1, -1, -1, -1, 2, 4, 6, 8,
                                                   };

        private static readonly int[] stepsizeTable = {
                                                          7, 8, 9, 10, 11, 12, 13, 14, 16, 17,
                                                          19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
                                                          50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
                                                          130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
                                                          337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
                                                          876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
                                                          2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
                                                          5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
                                                          15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
                                                      };

        public static short decodeADPCM(byte input, ref ADPCMState state)
        {
            int index = state.index;
            int step = stepsizeTable[index];
            int valpred = state.valprev;
            int delta = input;

            index += indexTable[delta];
            if (index < 0) index = 0;
            if (index > 88) index = 88;

            bool sign = ((delta & 8) == 8);
            delta = delta & 7;

            int vpdiff = step >> 3;
            if ((delta & 4) == 4) vpdiff += step;
            if ((delta & 2) == 2) vpdiff += step >> 1;
            if ((delta & 1) == 1) vpdiff += step >> 2;

            if (sign) valpred -= vpdiff;
            else valpred += vpdiff;

            if (valpred > 32767) valpred = 32767;
            else if (valpred < -32768) valpred = -32768;

            state.valprev = (short) valpred;
            state.index = (byte) index;
            return (short) valpred;
        }

        public static byte encodeADPCM(short input, ref ADPCMState state)
        {
            int index = state.index;
            int valpred = state.valprev;
            if (state.sample >= 65536)
            {
                state.sample = 0;
                index = 0;
                valpred = 0;
            }
            int step = stepsizeTable[index];
            int delta = input;
            int code;

            int diff = delta - valpred;

            if(diff >= 0) code = 0;
            else { code = 8; diff = -diff; }

            int tempstep = step;

            if(diff >= tempstep){
                code = (code | 4);
                diff = diff - tempstep;
            }

            tempstep = tempstep >> 1;

            if(diff >= tempstep){
                code = (code | 2);
                diff = diff - tempstep;
            }

            tempstep = tempstep >> 1;

            if(diff >= tempstep){
                code = (code | 1);
            }

            int diffq = step >> 3;
            if ((code & 4) == 4) diffq += step;
            if ((code & 2) == 2) diffq += step >> 1;
            if ((code & 1) == 1) diffq += step >> 2;

            bool sign = ((code & 8) == 8);

            if (sign) valpred -= diffq;
            else valpred += diffq;

            if (valpred > 32767) valpred = 32767;
            else if (valpred < -32768) valpred = -32768;
                        
            index += indexTable[code];
            
            if (index < 0) index = 0;
            if (index > 88) index = 88;                        

            state.valprev = (short) valpred;
            state.index = (byte) index;
            state.sample += 1;
            return (byte) (code & 15);
        }
    }
}