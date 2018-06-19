using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// http://www.shikadi.net/moddingwiki/RLE_Compression
namespace PersistenceList
{
    /// <summary>
    /// Basic implementation of Run-Length Encoding with the highest bit set for the Repeat code.
    /// The used run length is always (code & 0x7F).
    /// </summary>
    public class RleCompressionHighBitRepeat : RleImplementation<RleCompressionHighBitRepeat> { }

    /// <summary>
    /// Basic implementation of Run-Length Encoding with the highest bit set for the Copy code.
    /// The used run length is always (code & 0x7F).
    /// This uses the original GetCode/WriteCode functions but simply flips their "Repeat" boolean.
    /// </summary>
    public class RleCompressionHighBitCopy : RleImplementation<RleCompressionHighBitCopy>
    {
        protected override Boolean GetCode(Byte[] buffer, ref UInt32 inPtr, UInt32 bufferEnd, out Boolean isRepeat, out UInt32 amount)
        {
            Boolean success = base.GetCode(buffer, ref inPtr, bufferEnd, out isRepeat, out amount);
            isRepeat = !isRepeat;
            return success;
        }

        protected override Boolean WriteCode(Byte[] bufferOut, ref UInt32 outPtr, UInt32 bufferEnd, Boolean forRepeat, UInt32 amount)
        {
            return base.WriteCode(bufferOut, ref outPtr, bufferEnd, !forRepeat, amount);
        }
    }

    /// <summary>
    /// Basic Run-Length Encoding algorithm. Written by Maarten Meuris, aka Nyerguds.
    /// This class allows easy overriding of the code to read and write codes, to
    /// allow flexibility in subclassing the system for different RLE implementations.
    /// </summary>
    /// <typeparam name="T">
    /// The implementing class. This trick allows access to the internal type and its constructor from static functions
    /// in the superclass, giving the subclasses access to static functions that still use the specific subclass behaviour.
    /// </typeparam>
    public abstract class RleImplementation<T> where T : RleImplementation<T>, new()
    {
        #region overridables to tweak in subclasses
        /// <summary>Maximum amount of repeating bytes that can be stored in one code.</summary>
        public virtual UInt32 MaxRepeatValue { get { return 0x7F; } }
        /// <summary>Maximum amount of copied bytes that can be stored in one code.</summary>
        public virtual UInt32 MaxCopyValue { get { return 0x7F; } }

        /// <summary>
        /// Reads a code, determines the repeat / copy command and the amount of bytes to repeat / copy,
        /// and advances the read pointer to the location behind the read code.
        /// </summary>
        /// <param name="buffer">Input buffer.</param>
        /// <param name="inPtr">Input pointer.</param>
        /// <param name="bufferEnd">Exclusive end of buffer; first position that can no longer be read from.</param>
        /// <param name="isRepeat">Returns true for repeat code, false for copy code.</param>
        /// <param name="amount">Returns the amount to copy or repeat.</param>
        /// <returns>True if the read succeeded, false if it failed.</returns>
        protected virtual Boolean GetCode(Byte[] buffer, ref UInt32 inPtr, UInt32 bufferEnd, out Boolean isRepeat, out UInt32 amount)
        {
            if (inPtr >= bufferEnd)
            {
                isRepeat = false;
                amount = 0;
                return false;
            }
            Byte code = buffer[inPtr++];
            isRepeat = (code & 0x80) != 0;
            amount = (UInt32)(code & 0x7f);
            return true;
        }

        /// <summary>
        /// Writes the repeat / copy code to be put before the actual byte(s) to repeat / copy,
        /// and advances the write pointer to the location behind the written code.
        /// </summary>
        /// <param name="bufferOut">Output buffer to write to.</param>
        /// <param name="outPtr">Pointer for the output buffer.</param>
        /// <param name="bufferEnd">Exclusive end of buffer; first position that can no longer be written to.</param>
        /// <param name="forRepeat">True if this is a repeat code, false if this is a copy code.</param>
        /// <param name="amount">Amount to write into the repeat or copy code.</param>
        /// <returns>True if the write succeeded, false if it failed.</returns>
        protected virtual Boolean WriteCode(Byte[] bufferOut, ref UInt32 outPtr, UInt32 bufferEnd, Boolean forRepeat, UInt32 amount)
        {
            if (outPtr >= bufferEnd)
                return false;
            if (forRepeat)
                bufferOut[outPtr++] = (Byte)(amount | 0x80);
            else
                bufferOut[outPtr++] = (Byte)(amount);
            return true;
        }
        #endregion

        #region static functions
        /// <summary>
        /// Decodes RLE-encoded data.
        /// </summary>
        /// <param name="buffer">Buffer to decode.</param>
        /// <param name="startOffset">Start offset in buffer.</param>
        /// <param name="endOffset">End offset in buffer.</param>
        /// <param name="abortOnError">If true, any found command with amount "0" in it will cause the process to abort and return null.</param>
        /// <returns>A byte array of the given output size, filled with the decompressed data.</returns>
        public static Byte[] RleDecode(Byte[] buffer, UInt32? startOffset, UInt32? endOffset, Boolean abortOnError)
        {
            T rle = new T();
            Byte[] bufferOut = null;
            rle.RleDecodeData(buffer, null, null, ref bufferOut, abortOnError);
            return bufferOut;
        }

        /// <summary>
        /// Decodes RLE-encoded data.
        /// </summary>
        /// <param name="buffer">Buffer to decode.</param>
        /// <param name="startOffset">Start offset in buffer.</param>
        /// <param name="endOffset">End offset in buffer.</param>
        /// <param name="decompressedSize">The expected size of the decompressed data.</param>
        /// <param name="abortOnError">If true, any found command with amount "0" in it will cause the process to abort and return null.</param>
        /// <returns>A byte array of the given output size, filled with the decompressed data.</returns>
        public static Byte[] RleDecode(Byte[] buffer, UInt32? startOffset, UInt32? endOffset, Int32 decompressedSize, Boolean abortOnError)
        {
            T rle = new T();
            return rle.RleDecodeData(buffer, startOffset, endOffset, decompressedSize, abortOnError);
        }

        /// <summary>
        /// Decodes RLE-encoded data.
        /// </summary>
        /// <param name="buffer">Buffer to decode.</param>
        /// <param name="startOffset">Start offset in buffer.</param>
        /// <param name="endOffset">End offset in buffer.</param>
        /// <param name="bufferOut">Output array. Determines the maximum that can be decoded. If the given object is null it will be filled automatically.</param>
        /// <param name="abortOnError">If true, any found command with amount "0" in it will cause the process to abort and return null.</param>
        /// <returns>The amount of written bytes in bufferOut.</returns>
        public static Int32 RleDecode(Byte[] buffer, UInt32? startOffset, UInt32? endOffset, ref Byte[] bufferOut, Boolean abortOnError)
        {
            T rle = new T();
            return rle.RleDecodeData(buffer, startOffset, endOffset, ref bufferOut, abortOnError);
        }

        /// <summary>
        /// Applies Run-Length Encoding (RLE) to the given data.
        /// </summary>
        /// <param name="buffer">Input buffer.</param>
        /// <returns>The run-length encoded data.</returns>
        public static Byte[] RleEncode(Byte[] buffer)
        {
            T rle = new T();
            return rle.RleEncodeData(buffer);
        }
        #endregion

        #region public functions
        /// <summary>
        /// Decodes RLE-encoded data.
        /// </summary>
        /// <param name="buffer">Buffer to decode.</param>
        /// <param name="startOffset">Inclusive start offset in buffer. Defaults to 0.</param>
        /// <param name="endOffset">Exclusive end offset in buffer. Defaults to the buffer length.</param>
        /// <param name="decompressedSize">The expected size of the decompressed data.</param>
        /// <param name="abortOnError">If true, any found command with amount "0" in it will cause the process to abort and return null.</param>
        /// <returns>A byte array of the given output size, filled with the decompressed data, or null if abortOnError is enabled and an empty command was found.</returns>
        public Byte[] RleDecodeData(Byte[] buffer, UInt32? startOffset, UInt32? endOffset, Int32 decompressedSize, Boolean abortOnError)
        {
            Byte[] outputBuffer = new Byte[decompressedSize];
            Int32 result = this.RleDecodeData(buffer, startOffset, endOffset, ref outputBuffer, abortOnError);
            if (result == -1)
                return null;
            return outputBuffer;
        }

        /// <summary>
        /// Decodes RLE-encoded data.
        /// </summary>
        /// <param name="buffer">Buffer to decode.</param>
        /// <param name="startOffset">Inclusive start offset in buffer. Defaults to 0.</param>
        /// <param name="endOffset">Exclusive end offset in buffer. Defaults to the buffer length.</param>
        /// <param name="bufferOut">Output array. Determines the maximum that can be decoded.</param>
        /// <param name="abortOnError">If true, any found command with amount "0" in it will cause the process to abort and return -1.</param>
        /// <returns>The amount of written bytes in bufferOut.</returns>
        public Int32 RleDecodeData(Byte[] buffer, UInt32? startOffset, UInt32? endOffset, ref Byte[] bufferOut, Boolean abortOnError)
        {
            UInt32 inPtr = startOffset ?? 0;
            UInt32 inPtrEnd = endOffset.HasValue ? Math.Min(endOffset.Value, (UInt32)buffer.Length) : (UInt32)buffer.Length;

            UInt32 outPtr = 0;
            Boolean autoExpand = bufferOut == null;
            UInt32 bufLenOrig = inPtrEnd - inPtr;
            if (autoExpand)
                bufferOut = new Byte[bufLenOrig * 4];
            UInt32 maxOutLen = autoExpand ? UInt32.MaxValue : (UInt32)bufferOut.Length;
            Boolean error = false;

            while (inPtr < inPtrEnd && outPtr < maxOutLen)
            {
                // get next code
                UInt32 run;
                Boolean repeat;
                if (!this.GetCode(buffer, ref inPtr, inPtrEnd, out repeat, out run) || (run == 0 && abortOnError))
                {
                    error = true;
                    break;
                }
                //End ptr after run
                UInt32 runEnd = Math.Min(outPtr + run, maxOutLen);
                if (autoExpand && runEnd > bufferOut.Length)
                    bufferOut = ExpandBuffer(bufferOut, Math.Max(bufLenOrig, runEnd));
                // Repeat run
                if (repeat)
                {
                    if (inPtr >= inPtrEnd)
                        break;
                    Int32 repeatVal = buffer[inPtr++];
                    for (; outPtr < runEnd; outPtr++)
                        bufferOut[outPtr] = (Byte)repeatVal;
                    if (outPtr == maxOutLen)
                        break;
                }
                // Raw copy
                else
                {
                    Boolean abort = false;
                    for (; outPtr < runEnd; outPtr++)
                    {
                        if (inPtr >= inPtrEnd)
                        {
                            abort = true;
                            break;
                        }
                        Int32 data = buffer[inPtr++];
                        bufferOut[outPtr] = (Byte)data;
                    }
                    if (abort)
                        break;
                    if (outPtr == maxOutLen)
                        break;
                }
            }
            if (error)
                return -1;
            if (autoExpand)
            {
                Byte[] newBuf = new Byte[outPtr];
                Array.Copy(bufferOut, 0, newBuf, 0, outPtr);
                bufferOut = newBuf;
            }
            return (Int32)outPtr;
        }

        /// <summary>
        /// Applies Run-Length Encoding (RLE) to the given data. This particular function achieves especially good compression by only
        /// switching from a Copy command to a Repeat command if more than two repeating bytes are found, or if the maximum copy amount
        /// is reached. This avoids adding extra Copy command bytes after replacing two repeating bytes by a two-byte Repeat command.
        /// </summary>
        /// <param name="buffer">Input buffer.</param>
        /// <returns>The run-length encoded data.</returns>
        public Byte[] RleEncodeData(Byte[] buffer)
        {
            UInt32 inPtr = 0;
            UInt32 outPtr = 0;
            // Ensure big enough buffer. Sanity check will be done afterwards.
            UInt32 bufLen = (UInt32)((buffer.Length * 3) / 2);
            Byte[] bufferOut = new Byte[bufLen];

            // Retrieve these in advance to avoid extra calls to getters.
            // These are made customizable because some implementations support larger codes. Technically
            // neither run-length 0 nor 1 are useful for repeat codes (0 should not exist, 1 is identical to copy),
            // so the values are often decremented to allow storing one or two more bytes.
            // Some implementations also use these values as indicators for reading a larger value to repeat or copy.
            UInt32 maxRepeat = this.MaxRepeatValue;
            UInt32 maxCopy = this.MaxCopyValue;

            UInt32 len = (UInt32)buffer.Length;
            UInt32 detectedRepeat = 0;
            while (inPtr < len)
            {
                // Handle 2 cases: repeat was already detected, or a new repeat detect needs to be done.
                if (detectedRepeat >= 2 || (detectedRepeat = RepeatingAhead(buffer, len, inPtr, 2)) == 2)
                {
                    // Found more than 2 bytes. Worth compressing. Apply run-length encoding.
                    UInt32 start = inPtr;
                    UInt32 end = Math.Min(inPtr + maxRepeat, len);
                    Byte cur = buffer[inPtr];
                    // Already checked these in the RepeatingAhead function.
                    inPtr += detectedRepeat;
                    // Increase inptr to the last repeated.
                    for (; inPtr < end && buffer[inPtr] == cur; inPtr++) { }
                    // WriteCode is split off into a function to allow overriding it in specific implementations.
                    if (!this.WriteCode(bufferOut, ref outPtr, bufLen, true, (inPtr - start)) || outPtr + 1 >= bufLen)
                        break;
                    // Add value to repeat
                    bufferOut[outPtr++] = cur;
                    // Reset for next run
                    detectedRepeat = 0;
                }
                else
                {
                    Boolean abort = false;
                    // if detectedRepeat is not greater than 1 after writing a code,
                    // that means the maximum copy length was reached. Keep repeating
                    // until the copy is aborted for a repeat.
                    while (detectedRepeat == 1 && inPtr < len)
                    {
                        UInt32 start = inPtr;
                        // Normal non-repeat detection logic.
                        UInt32 end = Math.Min(inPtr + maxCopy, len);
                        UInt32 maxend = inPtr + maxCopy;
                        inPtr += detectedRepeat;
                        while (inPtr < end)
                        {
                            // detected bytes to compress after this one: abort.
                            detectedRepeat = RepeatingAhead(buffer, len, inPtr, 3);
                            // Only switch to Repeat when finding three repeated bytes: if the data
                            // behind a repeat of two is non-repeating, it adds an extra Copy command.
                            if (detectedRepeat == 3)
                                break;
                            // Optimise: apply a 1-byte or 2-byte skip to ptr right away.
                            inPtr += detectedRepeat;
                            // A detected repeat of two could make it go beyond the maximum accepted number of
                            // stored bytes per code. This fixes that. These repeating bytes are always saved as
                            // Repeat code, since a new command needs to be added after ending this one anyway.
                            // If you'd use the copy max amount instead, the 2-repeat would be cut in two Copy
                            // commands, wasting one byte if another repeating range would start after it.
                            if (inPtr > maxend)
                            {
                                inPtr -= detectedRepeat;
                                break;
                            }
                        }
                        UInt32 amount = inPtr - start;
                        if (amount == 0)
                        {
                            abort = true;
                            break;
                        }
                        // Need to reset this if the copy commands aborts for full size, so a last-detected repeat
                        // value of 2 at the end of a copy range isn't propagated to a new repeat command.
                        if (amount == maxCopy)
                            detectedRepeat = 0;
                        // WriteCode is split off into a function to allow overriding it in specific implementations.
                        abort = !this.WriteCode(bufferOut, ref outPtr, bufLen, false, amount) || outPtr + amount >= bufLen;
                        if (abort)
                            break;
                        // Add values to copy
                        for (UInt32 i = start; i < inPtr; i++)
                            bufferOut[outPtr++] = buffer[i];
                    }
                    if (abort)
                        break;
                }
            }
            Byte[] finalOut = new Byte[outPtr];
            Array.Copy(bufferOut, 0, finalOut, 0, outPtr);
            return finalOut;
        }
        #endregion

        #region internal tools

        protected Byte[] ExpandBuffer(Byte[] bufferOut, UInt32 expandSize)
        {
            Byte[] newBuf = new Byte[bufferOut.Length + expandSize];
            Array.Copy(bufferOut, 0, newBuf, 0, bufferOut.Length);
            return newBuf;
        }

        /// <summary>
        /// Checks if there are enough repeating bytes ahead.
        /// </summary>
        /// <param name="buffer">Input buffer.</param>
        /// <param name="max">The maximum offset to read inside the buffer.</param>
        /// <param name="ptr">The current read offset inside the buffer.</param>
        /// <param name="minAmount">Minimum amount of repeating bytes to search for.</param>
        /// <returns>The amount of detected repeating bytes.</returns>
        protected static UInt32 RepeatingAhead(Byte[] buffer, UInt32 max, UInt32 ptr, UInt32 minAmount)
        {
            Byte cur = buffer[ptr];
            for (UInt32 i = 1; i < minAmount; i++)
                if (ptr + i >= max || buffer[ptr + i] != cur)
                    return i;
            return minAmount;
        }
        #endregion
    }
}
