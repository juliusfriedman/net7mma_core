using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Runtime.CompilerServices;

namespace Media.Codec.Jpeg.Classes;

internal class ZigZag
{
    /// <summary>
    /// Special byte value to zero out elements during Sse/Avx shuffle intrinsics.
    /// </summary>
    private const byte _ = 0xff;

    #region Static Methods

    /// <summary>
    /// Gets shuffle vectors for <see cref="ApplyTransposingZigZagOrderingSsse3"/>
    /// zig zag implementation.
    /// </summary>
    private static ReadOnlySpan<byte> SseShuffleMasks =>
    [
        /* row0 - A0 B0 A1 A2 B1 C0 D0 C1 */
        // A
        0, 1, _, _, 2, 3, 4, 5, _, _, _, _, _, _, _, _,
        // B
        _, _, 0, 1, _, _, _, _, 2, 3, _, _, _, _, _, _,
        // C
        _, _, _, _, _, _, _, _, _, _, 0, 1, _, _, 2, 3,

        /* row1 - B2 A3 A4 B3 C2 D1 E0 F0 */
        // A
        _, _, 6, 7, 8, 9, _, _, _, _, _, _, _, _, _, _,
        // B
        4, 5, _, _, _, _, 6, 7, _, _, _, _, _, _, _, _,

        /* row2 - E1 D2 C3 B4 A5 A6 B5 C4 */
        // A
        _, _, _, _, _, _, _, _, 10, 11, 12, 13,  _,  _, _, _,
        // B
        _, _, _, _, _, _, 8, 9,  _,  _,  _,  _, 10, 11, _, _,
        // C
        _, _, _, _, 6, 7, _, _,  _,  _,  _,  _,  _,  _, 8, 9,

        /* row3 - D3 E2 F1 G0 H0 G1 F2 E3 */
        // E
        _, _, 4, 5, _, _, _, _, _, _, _, _, _, _, 6, 7,
        // F
        _, _, _, _, 2, 3, _, _, _, _, _, _, 4, 5, _, _,
        // G
        _, _, _, _, _, _, 0, 1, _, _, 2, 3, _, _, _, _,

        /* row4 - D4 C5 B6 A7 B7 C6 D5 E4 */
        // B
        _, _,  _,  _, 12, 13, _, _, 14, 15,  _,  _,  _,  _, _, _,
        // C
        _, _, 10, 11,  _,  _, _, _,  _,  _, 12, 13,  _,  _, _, _,
        // D
        8, 9,  _,  _,  _,  _, _, _,  _,  _,  _,  _, 10, 11, _, _,

        /* row5 - F3 G2 H1 H2 G3 F4 E5 D6 */
        // F
        6, 7, _, _, _, _, _, _, _, _, 8, 9, _, _, _, _,
        // G
        _, _, 4, 5, _, _, _, _, 6, 7, _, _, _, _, _, _,
        // H
        _, _, _, _, 2, 3, 4, 5, _, _, _, _, _, _, _, _,

        /* row6 - C7 D7 E6 F5 G4 H3 H4 G5 */
        // G
        _, _, _, _, _, _, _, _, 8, 9, _, _, _, _, 10, 11,
        // H
        _, _, _, _, _, _, _, _, _, _, 6, 7, 8, 9,  _,  _,

        /* row7 - F6 E7 F7 G6 H5 H6 G7 H7 */
        // F
        12, 13, _, _, 14, 15,  _,  _,  _,  _,  _,  _,  _,  _, _, _,
        // G
        _,  _, _, _,  _,  _, 12, 13,  _,  _,  _,  _, 14, 15, _, _,
        // H
        _,  _, _, _,  _,  _,  _,  _, 10, 11, 12, 13,  _,  _, 14, 15,
    ];

    /// <summary>
    /// Gets shuffle vectors for <see cref="ApplyTransposingZigZagOrderingAvx2"/>
    /// zig zag implementation.
    /// </summary>
    private static ReadOnlySpan<byte> AvxShuffleMasks =>
    [
        /* 01 */
        // [cr] crln_01_AB_CD
        0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   _, _, _, _,   1, 0, 0, 0,   2, 0, 0, 0,   4, 0, 0, 0,   5, 0, 0, 0,
        // (in) AB
        0, 1, 8, 9,   2, 3, 4, 5,   10, 11, _, _,   _, _, _, _,   12, 13, 2, 3,   4, 5, 14, 15,   _, _, _, _,   _, _, _, _,
        // (in) CD
        _, _, _, _,   _, _, _, _,   _, _, 0, 1,   8, 9, 2, 3,   _, _, _, _,   _, _, _, _,   0, 1, 10, 11,   _, _, _, _,
        // [cr] crln_01_23_EF_23_CD
        0, 0, 0, 0,   1, 0, 0, 0,   2, 0, 0, 0,   5, 0, 0, 0,   0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   5, 0, 0, 0,
        // (in) EF
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   0, 1, 8, 9,

        /* 23 */
        // [cr] crln_23_AB_23_45_GH
        2, 0, 0, 0,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   5, 0, 0, 0,
        // (in) AB
        _, _, _, _,   _, _, 8, 9,   2, 3, 4, 5,   10, 11, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // (in) CDe
        _, _, 12, 13,   6, 7, _, _,   _, _, _, _,   _, _, 8, 9,   14, 15, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // (in) EF
        2, 3, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 4, 5,   10, 11, _, _,   _, _, _, _,   12, 13, 6, 7,
        // (in) GH
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 0, 1,   8, 9, 2, 3,   _, _, _, _,

        /* 45 */
        // (in) AB
        _, _, _, _,   12, 13, 6, 7,   14, 15, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // [cr] crln_45_67_CD_45_EF
        2, 0, 0, 0,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   2, 0, 0, 0,   5, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,
        // (in) CD
        8, 9, 2, 3,   _, _, _, _,   _, _, 4, 5,   10, 11, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 12, 13,
        // (in) EF
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 0, 1,   6, 7, _, _,   _, _, _, _,   _, _, 8, 9,   2, 3, _, _,
        // (in) GH
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 4, 5,   10, 11, 12, 13,   6, 7, _, _,   _, _, _, _,

        /* 67 */
        // (in) CD
        6, 7, 14, 15,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // [cr] crln_67_EF_67_GH
        2, 0, 0, 0,   3, 0, 0, 0,   5, 0, 0, 0,   6, 0, 0, 0,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   _, _, _, _,
        // (in) EF
        _, _, _, _,   4, 5, 14, 15,   _, _, _, _,   _, _, _, _,   8, 9, 2, 3,   10, 11, _, _,   _, _, _, _,   _, _, _, _,
        // (in) GH
        _, _, _, _,   _, _, _, _,   0, 1, 10, 11,   12, 13, 2, 3,   _, _, _, _,   _, _, 0, 1,   6, 7, 8, 9,   2, 3, 10, 11,
    ];

    /// <summary>
    /// Applies zig zag ordering for given 8x8 matrix using SSE cpu intrinsics.
    /// </summary>
    /// <param name="block">Input matrix.</param>
    public static void ApplyTransposingZigZagOrderingSsse3(Block block)
    {
        ref readonly byte shuffleVectorsPtr = ref MemoryMarshal.GetReference(SseShuffleMasks);

        Vector128<byte> rowA = block.V0.AsByte();
        Vector128<byte> rowB = block.V1.AsByte();
        Vector128<byte> rowC = block.V2.AsByte();
        Vector128<byte> rowD = block.V3.AsByte();
        Vector128<byte> rowE = block.V4.AsByte();
        Vector128<byte> rowF = block.V5.AsByte();
        Vector128<byte> rowG = block.V6.AsByte();
        Vector128<byte> rowH = block.V7.AsByte();

        // row0 - A0 B0 A1 A2 B1 C0 D0 C1
        Vector128<short> row0_A = Ssse3.Shuffle(rowA, Vector128.LoadUnsafe(in shuffleVectorsPtr)).AsInt16();
        Vector128<short> row0_B = Ssse3.Shuffle(rowB, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 16))).AsInt16();
        Vector128<short> row0_C = Ssse3.Shuffle(rowC, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 32))).AsInt16();
        Vector128<short> row0 = Sse2.Or(Sse2.Or(row0_A, row0_B), row0_C);
        row0 = Sse2.Insert(row0.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 0), 6).AsInt16();

        // row1 - B2 A3 A4 B3 C2 D1 E0 F0
        Vector128<short> row1_A = Ssse3.Shuffle(rowA, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 48))).AsInt16();
        Vector128<short> row1_B = Ssse3.Shuffle(rowB, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 64))).AsInt16();
        Vector128<short> row1 = Sse2.Or(row1_A, row1_B);
        row1 = Sse2.Insert(row1.AsUInt16(), Sse2.Extract(rowC.AsUInt16(), 2), 4).AsInt16();
        row1 = Sse2.Insert(row1.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 1), 5).AsInt16();
        row1 = Sse2.Insert(row1.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 0), 6).AsInt16();
        row1 = Sse2.Insert(row1.AsUInt16(), Sse2.Extract(rowF.AsUInt16(), 0), 7).AsInt16();

        // row2 - E1 D2 C3 B4 A5 A6 B5 C4
        Vector128<short> row2_A = Ssse3.Shuffle(rowA, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 80))).AsInt16();
        Vector128<short> row2_B = Ssse3.Shuffle(rowB, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 96))).AsInt16();
        Vector128<short> row2_C = Ssse3.Shuffle(rowC, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 112))).AsInt16();
        Vector128<short> row2 = Sse2.Or(Sse2.Or(row2_A, row2_B), row2_C);
        row2 = Sse2.Insert(row2.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 2), 1).AsInt16();
        row2 = Sse2.Insert(row2.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 1), 0).AsInt16();

        // row3 - D3 E2 F1 G0 H0 G1 F2 E3
        Vector128<short> row3_E = Ssse3.Shuffle(rowE, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 128))).AsInt16();
        Vector128<short> row3_F = Ssse3.Shuffle(rowF, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 144))).AsInt16();
        Vector128<short> row3_G = Ssse3.Shuffle(rowG, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 160))).AsInt16();
        Vector128<short> row3 = Sse2.Or(Sse2.Or(row3_E, row3_F), row3_G);
        row3 = Sse2.Insert(row3.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 3), 0).AsInt16();
        row3 = Sse2.Insert(row3.AsUInt16(), Sse2.Extract(rowH.AsUInt16(), 0), 4).AsInt16();

        // row4 - D4 C5 B6 A7 B7 C6 D5 E4
        Vector128<short> row4_B = Ssse3.Shuffle(rowB, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 176))).AsInt16();
        Vector128<short> row4_C = Ssse3.Shuffle(rowC, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 192))).AsInt16();
        Vector128<short> row4_D = Ssse3.Shuffle(rowD, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 208))).AsInt16();
        Vector128<short> row4 = Sse2.Or(Sse2.Or(row4_B, row4_C), row4_D);
        row4 = Sse2.Insert(row4.AsUInt16(), Sse2.Extract(rowA.AsUInt16(), 7), 3).AsInt16();
        row4 = Sse2.Insert(row4.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 4), 7).AsInt16();

        // row5 - F3 G2 H1 H2 G3 F4 E5 D6
        Vector128<short> row5_F = Ssse3.Shuffle(rowF, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 224))).AsInt16();
        Vector128<short> row5_G = Ssse3.Shuffle(rowG, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 240))).AsInt16();
        Vector128<short> row5_H = Ssse3.Shuffle(rowH, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 256))).AsInt16();
        Vector128<short> row5 = Sse2.Or(Sse2.Or(row5_F, row5_G), row5_H);
        row5 = Sse2.Insert(row5.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 6), 7).AsInt16();
        row5 = Sse2.Insert(row5.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 5), 6).AsInt16();

        // row6 - C7 D7 E6 F5 G4 H3 H4 G5
        Vector128<short> row6_G = Ssse3.Shuffle(rowG, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 272))).AsInt16();
        Vector128<short> row6_H = Ssse3.Shuffle(rowH, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 288))).AsInt16();
        Vector128<short> row6 = Sse2.Or(row6_G, row6_H);
        row6 = Sse2.Insert(row6.AsUInt16(), Sse2.Extract(rowC.AsUInt16(), 7), 0).AsInt16();
        row6 = Sse2.Insert(row6.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 7), 1).AsInt16();
        row6 = Sse2.Insert(row6.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 6), 2).AsInt16();
        row6 = Sse2.Insert(row6.AsUInt16(), Sse2.Extract(rowF.AsUInt16(), 5), 3).AsInt16();

        // row7 - F6 E7 F7 G6 H5 H6 G7 H7
        Vector128<short> row7_F = Ssse3.Shuffle(rowF, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 304))).AsInt16();
        Vector128<short> row7_G = Ssse3.Shuffle(rowG, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 320))).AsInt16();
        Vector128<short> row7_H = Ssse3.Shuffle(rowH, Vector128.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(SseShuffleMasks), 336))).AsInt16();
        Vector128<short> row7 = Sse2.Or(Sse2.Or(row7_F, row7_G), row7_H);
        row7 = Sse2.Insert(row7.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 7), 1).AsInt16();

        block.V0 = row0;
        block.V1 = row1;
        block.V2 = row2;
        block.V3 = row3;
        block.V4 = row4;
        block.V5 = row5;
        block.V6 = row6;
        block.V7 = row7;
    }

    /// <summary>
    /// Applies zig zag ordering for given 8x8 matrix using AVX cpu intrinsics.
    /// </summary>
    /// <param name="block">Input matrix.</param>
    public static void ApplyTransposingZigZagOrderingAvx2(Block block)
    {
        ref readonly byte shuffleVectorsPtr = ref MemoryMarshal.GetReference(AvxShuffleMasks);

        Vector256<byte> rowAB = block.V01.AsByte();
        Vector256<byte> rowCD = block.V23.AsByte();
        Vector256<byte> rowEF = block.V45.AsByte();
        Vector256<byte> rowGH = block.V67.AsByte();

        

        /* row01 - A0 B0 A1 A2 B1 C0 D0 C1 | B2 A3 A4 B3 C2 D1 E0 F0 */
        Vector256<int> crln_01_AB_CD = Vector256.LoadUnsafe(in shuffleVectorsPtr).AsInt32();
        Vector256<byte> row01_AB = Avx2.PermuteVar8x32(rowAB.AsInt32(), crln_01_AB_CD).AsByte();
        row01_AB = Avx2.Shuffle(row01_AB, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 32))).AsByte();
        Vector256<byte> row01_CD = Avx2.PermuteVar8x32(rowCD.AsInt32(), crln_01_AB_CD).AsByte();
        row01_CD = Avx2.Shuffle(row01_CD, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 64))).AsByte();
        Vector256<int> crln_01_23_EF_23_CD = Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 96)).AsInt32();
        Vector256<byte> row01_23_EF = Avx2.PermuteVar8x32(rowEF.AsInt32(), crln_01_23_EF_23_CD).AsByte();
        Vector256<byte> row01_EF = Avx2.Shuffle(row01_23_EF, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 128)).AsByte());

        Vector256<byte> row01 = Avx2.Or(row01_AB, Avx2.Or(row01_CD, row01_EF));

        /* row23 - E1 D2 C3 B4 A5 A6 B5 C4 | D3 E2 F1 G0 H0 G1 F2 E3 */
        Vector256<int> crln_23_AB_23_45_GH = Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 160)).AsInt32();
        Vector256<byte> row23_45_AB = Avx2.PermuteVar8x32(rowAB.AsInt32(), crln_23_AB_23_45_GH).AsByte();
        Vector256<byte> row23_AB = Avx2.Shuffle(row23_45_AB, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 192))).AsByte();
        Vector256<byte> row23_CD = Avx2.PermuteVar8x32(rowCD.AsInt32(), crln_01_23_EF_23_CD).AsByte();
        row23_CD = Avx2.Shuffle(row23_CD, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 224))).AsByte();
        Vector256<byte> row23_EF = Avx2.Shuffle(row01_23_EF, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 256))).AsByte();
        Vector256<byte> row23_45_GH = Avx2.PermuteVar8x32(rowGH.AsInt32(), crln_23_AB_23_45_GH).AsByte();
        Vector256<byte> row23_GH = Avx2.Shuffle(row23_45_GH, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 288))).AsByte();

        Vector256<byte> row23 = Avx2.Or(Avx2.Or(row23_AB, row23_CD), Avx2.Or(row23_EF, row23_GH));

        /* row45 - D4 C5 B6 A7 B7 C6 D5 E4 | F3 G2 H1 H2 G3 F4 E5 D6 */
        Vector256<byte> row45_AB = Avx2.Shuffle(row23_45_AB, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 320))).AsByte();
        Vector256<int> crln_45_67_CD_45_EF = Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 352)).AsInt32();
        Vector256<byte> row45_67_CD = Avx2.PermuteVar8x32(rowCD.AsInt32(), crln_45_67_CD_45_EF).AsByte();
        Vector256<byte> row45_CD = Avx2.Shuffle(row45_67_CD, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 384))).AsByte();
        Vector256<byte> row45_EF = Avx2.PermuteVar8x32(rowEF.AsInt32(), crln_45_67_CD_45_EF).AsByte();
        row45_EF = Avx2.Shuffle(row45_EF, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 416))).AsByte();
        Vector256<byte> row45_GH = Avx2.Shuffle(row23_45_GH, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 448))).AsByte();

        Vector256<byte> row45 = Avx2.Or(Avx2.Or(row45_AB, row45_CD), Avx2.Or(row45_EF, row45_GH));

        /* row67 - C7 D7 E6 F5 G4 H3 H4 G5 | F6 E7 F7 G6 H5 H6 G7 H7 */
        Vector256<byte> row67_CD = Avx2.Shuffle(row45_67_CD, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 480))).AsByte();
        Vector256<int> crln_67_EF_67_GH = Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 512)).AsInt32();
        Vector256<byte> row67_EF = Avx2.PermuteVar8x32(rowEF.AsInt32(), crln_67_EF_67_GH).AsByte();
        row67_EF = Avx2.Shuffle(row67_EF, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 544))).AsByte();
        Vector256<byte> row67_GH = Avx2.PermuteVar8x32(rowGH.AsInt32(), crln_67_EF_67_GH).AsByte();
        row67_GH = Avx2.Shuffle(row67_GH, Vector256.LoadUnsafe(in Unsafe.Add(ref MemoryMarshal.GetReference(AvxShuffleMasks), 576))).AsByte();

        Vector256<byte> row67 = Avx2.Or(row67_CD, Avx2.Or(row67_EF, row67_GH));

        block.V01 = row01.AsInt16();
        block.V23 = row23.AsInt16();
        block.V45 = row45.AsInt16();
        block.V67 = row67.AsInt16();
    }

    #endregion

    /// <summary>
    /// Gets span of zig-zag ordering indices.
    /// </summary>
    /// <remarks>
    /// When reading corrupted data, the Huffman decoders could attempt
    /// to reference an entry beyond the end of this array (if the decoded
    /// zero run length reaches past the end of the block).  To prevent
    /// wild stores without adding an inner-loop test, we put some extra
    /// "63"s after the real entries.  This will cause the extra coefficient
    /// to be stored in location 63 of the block, not somewhere random.
    /// The worst case would be a run-length of 15, which means we need 16
    /// fake entries.
    /// </remarks>
    public static ReadOnlySpan<byte> ZigZagOrder =>
    [
        0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,

        // Extra entries for safety in decoder
        63, 63, 63, 63, 63, 63, 63, 63,
        63, 63, 63, 63, 63, 63, 63, 63
    ];

    /// <summary>
    /// Gets span of zig-zag with fused transpose step ordering indices.
    /// </summary>
    /// <remarks>
    /// When reading corrupted data, the Huffman decoders could attempt
    /// to reference an entry beyond the end of this array (if the decoded
    /// zero run length reaches past the end of the block).  To prevent
    /// wild stores without adding an inner-loop test, we put some extra
    /// "63"s after the real entries.  This will cause the extra coefficient
    /// to be stored in location 63 of the block, not somewhere random.
    /// The worst case would be a run-length of 15, which means we need 16
    /// fake entries.
    /// </remarks>
    public static ReadOnlySpan<byte> TransposingOrder =>
    [
        0,  8,  1,  2,  9,  16, 24, 17,
        10, 3,  4,  11, 18, 25, 32, 40,
        33, 26, 19, 12, 5,  6,  13, 20,
        27, 34, 41, 48, 56, 49, 42, 35,
        28, 21, 14, 7,  15, 22, 29, 36,
        43, 50, 57, 58, 51, 44, 37, 30,
        23, 31, 38, 45, 52, 59, 60, 53,
        46, 39, 47, 54, 61, 62, 55, 63,

        // Extra entries for safety in decoder
        63, 63, 63, 63, 63, 63, 63, 63,
        63, 63, 63, 63, 63, 63, 63, 63
    ];
}
