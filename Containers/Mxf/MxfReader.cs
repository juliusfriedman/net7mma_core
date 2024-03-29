﻿/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://github.com/juliusfriedman/net7mma_core
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. https://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */
using Media.Container;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Media.Containers.Mxf
{
    /// <summary>
    /// Represents the logic necessary to read files compatible with SMPTE 377-1-2009 (377M-2004) [the Material Exchange Format]
    /// The reader is also compatible with OMF and AAF Files.
    /// </summary>
    public class MxfReader : MediaFileStream, IMediaContainer
    {
        /// <summary>
        /// Defines common UniversalLabel's which are required to parse the container format.        
        /// </summary>
        /// <notes>Several entries could be removed in favor of an enumertion GenericDescriptorType</notes>
        public static class UniversalLabel
        {

            /* 
            - Identifier Format Information -
                1 Object Identifier 06h
                2 Label size 0Eh
                3 Designator 2Bh ISO, ORG
                4 Designator 34h SMPTE
                5 Registry Category Designator 02h KLV Sets & Packs
                6 Registry Designator 05h Defined-Length Packs (no length fields)
                7 Structure Designator 01h Set / Pack registry
                8 Version Number vvh Registry Version in which the specific Key
                    first appeared
                9 Item Designator 0Dh Organizationally registered
                10 Organization 01h AAF
                11 Application 02h MXF File Structure
                12 Structure Version 01h Version 1
                13 Structure Kind 01h MXF File Structure sets & packs
                14 Set / Pack Kind Sections 7.2, 7.3, 7.4 Header Partition, Body Partition or Footer
                    Partition
                15 Partition Status Sections 7.2, 7.3, 7.4 Open and Incomplete (01h)
                    Closed and Incomplete (02h)
                    Open and Complete (03h)
                    Closed and Complete (04h)
                16 Reserved 00h
                Byte 15 provides four alternate Partition Pack Key values for the combinations provided by an Open or
                Closed Partition, and Complete or Incomplete Header Metadata as defined in Section 6.2.3.
                Other MXF standards that define other types of Partitions may define additional values of byte 15.
            Note: SMPTE 410M is such an MXF standard.
             */


            //Header, Body and Footer Klv - The last 2 bytes indicate type and open or closed.                                                         //Type, //Status, //Reserved
            public static Guid PartitionPack = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x02, 0x00, 0x00 });

            //For tracks the last 4 are the trackId 
            public static Guid EssenceElement = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0D, 0x01, 0x03, 0x01, 0, 0, 0, 0 });

            //KnownUnknown?
            //0x06,0x0E,0x2B,0x34,0x04,0x01,0x01,0x0A,0x01,0x01,0x02,0x01,0x01,0x00,0x00,0x00

            /*
             8.2 Generic Universal Label for All Operational Patterns
            The value of the Operational Pattern UL used to identify any MXF Operational Pattern shall be defined in the
            table below.
            Table 9 – Value of the MXF Operational Pattern Identification Universal Label
             Byte No. Description Value (hex) Meaning
             1 Object Identifier 06h
             2 Label size 0Eh
             3 Designator 2Bh ISO, ORG
             4 Designator 34h SMPTE
             5 Registry Category Designator 04h Labels
             6 Registry Designator 01h Labels
             7 Structure Designator 01h Labels
             8 Version Number vvh Registry Version in which the Label first appeared
             9 Item Designator 0Dh Organizationally Registered
             10 Organization 01h AAF Association
             11 Application 02h Operational Patterns
             12 Structure Version 01h Version 1
             13 Operational Pattern Definition xxh Item Complexity
             14~16 Definition depends on byte 13 xxh
             */

            public static Guid OperationalPattern = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0, 0, 0, 0 });

            //Value 8 may be different >0 ? - ff
            //public static Guid PrimerPack = new Guid(new byte[] { 0x06, 0x0E, 0x2B , 0x34 , 0x02 , 0x05 , 0x01 , 0x01 , 0x0D , 0x01 , 0x02 , 0x01 , 0x01 , 0x05 , 0x01 , 0x00 });

            public static Guid RandomIndexPack = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0D, 0x01, 0x02, 0x01, 0x01, 0x11, 0x01, 0x00 });

            public static Guid PartitionMetadata = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0d, 0x01, 0x02, 0x01, 0x01, 0x04, 0x04, 0x00 });

            public static Guid EncryptedEssenceContainer = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x04, 0x01, 0x01, 0x07, 0x0d, 0x01, 0x03, 0x01, 0x02, 0x0b, 0x01, 0x00 });

            public static Guid FillerPack = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x01, 0x01, 0x01, 0x02, 0x03, 0x01, 0x02, 0x10, 0x01, 0x00, 0x00, 0x00 });

            //Rp210 version number byte (8) is different
            public static Guid LegacyFillerPack = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x01, 0x01, 0x01, 0x01, 0x03, 0x01, 0x02, 0x10, 0x01, 0x00, 0x00, 0x00 });

            public static Guid Index = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x02, 0x01, 0x01, 0x10, 0x01, 0x00 });

            //Could be top level with an enum defining all 

            public static Guid StructuralMetadata = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00 });

            //Structural MetaData Sets (Also Generic by label)

            public static Guid InterchangeObject = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00 });

            public static Guid StructuralComponent = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x02, 0x00 });

            public static Guid Segment = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x03, 0x00 });

            public static Guid Event = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x06, 0x00 });

            public static Guid CommentMarker = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x08, 0x00 });

            public static Guid Preface = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x2F, 0x00 });

            public static Guid Identification = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x30, 0x00 });

            public static Guid ContentStorage = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x18, 0x00 });

            //Is almost like a FileDescriptor but useless...
            public static Guid EssenceContainerData = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x23, 0x00 });

            //Incase no other one is present.
            public static Guid GenericDescriptor = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x24, 0x00 });

            //Must be instance of FileDescriptor ... --- !!!!
            public static Guid FileDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x25, 0x00 });

            public static Guid GenericPackage = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x34, 0x00 });

            public static Guid SubDescriptor = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x59, 0x00 });

            public static Guid MaterialPackage = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x36, 0x00 });

            public static Guid SourcePackage = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x37, 0x00 });

            public static Guid TimelineTrack = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x3B, 0x00 });

            public static Guid GenericTrack = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x38, 0x00 });

            public static Guid EventTrack = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x39, 0x00 });

            public static Guid StaticTrack = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x3A, 0x00 });

            public static Guid Sequence = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x0F, 0x00 });

            public static Guid SourceClip = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x11, 0x00 });

            public static Guid TimecodeComponent = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x14, 0x00 });

            public static Guid DMSegment = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x41, 0x00 });

            public static Guid DMSourceClip = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x45, 0x00 });

            public static Guid Filler = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x09, 0x00 });

            public static Guid PackageMarkerObject = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x60, 0x00 });

            // Also 6th Byte allowed to be 13 (Note 1: According to SMPTE 336M, the xxh entry in Table 16 has the value of 13h for BER long or short form encoded length and 53h for 2-byte length)

            public static Guid FillerBer = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x13, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x09, 0x00 });

            public static Guid PackageMarkerObjectBer = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x13, 0x01, 0x01, 0x0D, 0x01, 0x01, 0x01, 0x01, 0x01, 0x60, 0x00 });

            //

            public static Guid GenericPictureEssenceDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x27, 0x00 });

            public static Guid CDCIEssenceDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x28, 0x00 });

            public static Guid RGBAEssenceDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x29, 0x00 });

            public static Guid GenericSoundEssenceDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x42, 0x00 });

            public static Guid GenericDataEssenceDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x43, 0x00 });

            //Has Codec UL?

            public static Guid MultipleDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x44, 0x00 });

            public static Guid NetworkLocator = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x32, 0x00 });

            public static Guid TextLocator = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x33, 0x00 });

            public static Guid ApplicationPlugInObject = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x61, 0x00 });

            public static Guid ApplicationReferencedObject = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x62, 0x00 });

            //6th byte allowed to be 13 // Also 6th Byte allowed to be 13 (Note 1: According to SMPTE 336M, the xxh entry in Table 16 has the value of 13h for BER long or short form encoded length and 53h for 2-byte length)

            public static Guid ApplicationPlugInObjectBer = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x13, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x61, 0x00 });

            public static Guid ApplicationReferencedObjectBer = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x13, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x62, 0x00 });

            //

            public static Guid ApplicationObject = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x66, 0x00 });

            //

            public static Guid MPEG2VideoDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x51, 0x00 });

            public static Guid WaveAudioDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x48, 0x00 });

            public static Guid AES3PCMDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x47, 0x00 });

            public static Guid J2KPictureDescriptor = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x02, 0x53, 0x01, 0x01, 0x0d, 0x01, 0x01, 0x01, 0x01, 0x01, 0x5a, 0x00 });

            //DataDefinitions...
            public static Guid DataDefinitionVideo = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x01, 0x03, 0x02, 0x02, 0x01, 0x00, 0x00, 0x00 });

            public static Guid DataDefinitionVideoLegacy = new(new byte[] { 0x80, 0x7D, 0x00, 0x60, 0x08, 0x14, 0x3E, 0x6F, 0x6F, 0x3C, 0x8C, 0xE1, 0x6C, 0xEF, 0x11, 0xD2 });

            public static Guid DataDefinitionAudio = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x01, 0x03, 0x02, 0x02, 0x02, 0x00, 0x00, 0x00 });

            public static Guid DataDefinitionAudioLegacy = new(new byte[] { 0x80, 0x7D, 0x00, 0x60, 0x08, 0x14, 0x3E, 0x6F, 0x78, 0xE1, 0xEB, 0xE1, 0x6C, 0xEF, 0x11, 0xD2 });

            //EssenceContainer UL's

            public static Guid Mpeg2 = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x04, 0x01, 0x01, 0x02, 0x0d, 0x01, 0x03, 0x01, 0x02, 0x04, 0x60, 0x01 });

            //SMPTE D-10 Mapping
            public static Guid Mpeg2Alt = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x04, 0x01, 0x01, 0x01, 0x0d, 0x01, 0x03, 0x01, 0x02, 0x01, 0x00, 0x00 });

            public static Guid DV = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x04, 0x01, 0x01, 0x01, 0x0d, 0x01, 0x03, 0x01, 0x02, 0x02, 0x41, 0x01 });

            public static Guid UncompressedPicture = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x04, 0x01, 0x01, 0x01, 0x0d, 0x01, 0x03, 0x01, 0x02, 0x05, 0x00, 0x00 });

            //Codecs

            public static Guid MPEG2_XDCam = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x03, 0x04, 0x01, 0x02, 0x02, 0x01, 0x04, 0x03, 0x00 });

            public static Guid MPEG2_ML = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x03, 0x04, 0x01, 0x02, 0x02, 0x01, 0x01, 0x11, 0x00 });

            public static Guid MPEG2_D10_PAL = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x01, 0x02, 0x02, 0x01, 0x02, 0x01, 0x01 });

            public static Guid MPEG2_HL = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x03, 0x04, 0x01, 0x02, 0x02, 0x01, 0x03, 0x03, 0x00 });

            public static Guid MPEG2_HL_422_I = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x03, 0x04, 0x01, 0x02, 0x02, 0x01, 0x04, 0x02, 0x00 });

            public static Guid Mpeg4 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x03, 0x04, 0x01, 0x02, 0x02, 0x01, 0x20, 0x02, 0x03 });

            //DDVIDEO
            public static Guid DV_25_PAL = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x01, 0x02, 0x02, 0x02, 0x01, 0x02, 0x00 });

            //Jpeg
            /*
             This "dark" EssenceContainer contains JPEG and uncompressed audio in an undocumented interleaved fashion. Fields of video are compressed as JPEG images and arranged in "clumps" of 18 fields, followed by uncompressed audio data, followed by the next clump of JPEGs and so on.
             */

            //Sony Long GOP?
            //06-0E-2B-34-02-05-01-01-0D-01-03-01-04-01-01-00

            //?
            //06 0e 2b 34 01 01 01 05 03 01 02 20 01 00 00 00

            public static Guid Dark = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x02, 0x05, 0x01, 0x01, 0x0d, 0x01, 0x03, 0x01, 0x18, 0x01, 0x02, 0x01 });

            public static Guid JPEG2000 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x07, 0x04, 0x01, 0x02, 0x02, 0x03, 0x01, 0x01, 0x00 });

            public static Guid Raw = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x01, 0x02, 0x01, 0x7F, 0x00, 0x00, 0x00 });

            public static Guid Raw_422 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0A, 0x04, 0x01, 0x02, 0x01, 0x01, 0x02, 0x01, 0x00 });

            public static Guid VC3_DNXD = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x01, 0x02, 0x02, 0x03, 0x02, 0x00, 0x00 });

            public static Guid VC3_DNXD_Alt = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x01, 0x02, 0x02, 0x71, 0x00, 0x00, 0x00 });

            public static Guid VC3_DNXD_Legacy = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x0E, 0x04, 0x02, 0x01, 0x02, 0x04, 0x01, 0x00 });

            //H.264
            //SPS and PPS In Band
            public static Guid AVC_SPSPPS = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0A, 0x04, 0x01, 0x02, 0x02, 0x01, 0x31, 0x00, 0x00 });
            //Intra
            public static Guid AVC_INTRA = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0A, 0x04, 0x01, 0x02, 0x02, 0x01, 0x32, 0x00, 0x00 });

            public static Guid V210 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0A, 0x04, 0x01, 0x02, 0x01, 0x01, 0x02, 0x02, 0x00 });

            //Sound
            public static Guid Mpeg2_AAC_DTS_Legacy = new(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x04, 0x01, 0x01, 0x03, 0x04, 0x02, 0x02, 0x02, 0x03, 0x03, 0x01, 0x00 });

            //Uncompressed
            public static Guid PCM_S16LE_1 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x02, 0x02, 0x01, 0x00, 0, 0, 0 });

            public static Guid PCM_S16LE_2 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x02, 0x02, 0x01, 0x7F, 0, 0, 0 });

            public static Guid PCM_S16LE_3 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x02, 0x02, 0x01, 0x01, 0, 0, 0 });

            public static Guid PCM_S16BE = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x07, 0x04, 0x02, 0x02, 0x01, 0x7E, 0, 0, 0 });

            public static Guid PCM_ALAW = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x04, 0x04, 0x02, 0x02, 0x02, 0x03, 0x01, 0x01, 0x00 });

            public static Guid AC3 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x02, 0x02, 0x02, 0x03, 0x02, 0x01, 0x00 });

            //Mp3
            public static Guid MP2 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x02, 0x02, 0x02, 0x03, 0x02, 0x05, 0x00 });

            public static Guid Dolby_E = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x01, 0x04, 0x02, 0x02, 0x02, 0x03, 0x02, 0x1C, 0x00 });

            //Pixel Formats

            public static Guid PixelFormat = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0A, 0x04, 0x01, 0x02, 0x01, 0, 0, 0, 0 });

            public static Guid PixelFormatUYUV_422 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0A, 0x04, 0x01, 0x02, 0x01, 0x01, 0x02, 0x01, 0x01 });

            public static Guid PixelFormatYUYV_422 = new(new byte[] { 0x06, 0x0E, 0x2B, 0x34, 0x04, 0x01, 0x01, 0x0A, 0x04, 0x01, 0x02, 0x01, 0x01, 0x02, 0x01, 0x02 });
        }

        //Byte 14 of any Universal Label for a PartitionPack
        public enum PartitionKind
        {
            Unknown = 0,
            Other = 1,
            Header = 2,
            Body = 3,
            Footer = 4,
            Primer = 5 //Not in standard but does work
        }

        //Byte 15 of any Universal Label for a PartitionPack
        public enum PartitionStatus
        {
            Unknown = 0,
            OpenAndIncomplete = 1,
            ClosedAndIncomplete = 2,
            OpenAndComplete = 3,
            ClosedAndComplete = 4
        }

        //Byte 13 of operational pattern
        public enum OperationalPatternItemComplexity
        {
            Unknown = 0,
            SingleItem = 1,
            PlayListItems = 2,
            EditItems = 3
            //10h - 7h = Specialized
        }

        //Byte 4 of a Universal Label
        public enum Category
        {
            Unknown,
            Dictionary,
            Group,
            Wrapper,
            Label,
            Private,
            Reserved
        }

        ////Byte 14 of operational pattern under certain conditions
        //public enum OperationalPatternPackageComplexity
        //{
        //    Unknown = 0,
        //    SinglePackage = 1,
        //    GangedPackages = 2,
        //    AlternatePackages = 3
        //}

        //Byte 15 in a OperationalPattern is bit wise
        // 0 Value = 1 Marker bit
        // 1 = 0, internal Essence, 1 = external Essence
        // 2 = 0 = stream file, 1 = non stream file
        // 3 = 0 = uniTack, 1 = multiTrack
        // 7 - 4 Reserved for future use...

        #region Constants

        private const int IdentifierSize = 16, MinimumSizeLength = 1, MinimumSize = IdentifierSize + MinimumSizeLength, UniqueIdBytes = 12, MultiByteLength = 0x80; //128

        private const string PictureTrack = "Picture Track", AudioTrack = "Audio Track", TextTrack = "Text Track", TimecodeTrack = "Timecode Track", DataTrack = "Data Track";

        #endregion

        #region Statics

        /*
         UL Designator: The first 8 bytes of a SMPTE Universal Label. They identify the register, its minor and major
        versions, and may also convey other information that is used by the KLV encoding protocol. The register is
        the normative reference for the values of the 8 bytes.
        Note: This is different from the definition of UL Designator in SMPTE 336M. In that standard, the term UL Designator
        refers to only bytes 3 to 8 of the Label; the first two bytes (i.e. byte 1 and 2) are referred to as the Label Header.
         * 
         * Note 1: According to SMPTE 336M, the xxh entry in Table 16 has the value of 13h for BER long or short form encoded length and 53h for 2-byte length.
         */

        public static Category GetCategory(Node node)
        {
            return node is null ? throw new ArgumentNullException("node") : (Category)node.Identifier[4];
        }

        public static PartitionKind GetPartitionKind(Node node)
        {
            if (node is null) throw new ArgumentNullException("node");

            //Should check node starts with ParitionPack...
            //if (false == CompareUL(node.Identifier, UniversalLabel.PartitionPack.ToByteArray())) return PartitionKind.Unknown;

            return (PartitionKind)node.Identifier[13];
        }

        //Should check node starts with ParitionPack?
        public static PartitionStatus GetPartitionStatus(Node node)
        {
            if (node is null) throw new ArgumentNullException("node");

            //Should check node starts with ParitionPack...
            //if (false == CompareUL(node.Identifier, UniversalLabel.PartitionPack.ToByteArray())) return PartitionStatus.Unknown;

            return (PartitionStatus)node.Identifier[14];
        }

        private static bool CompareUL(Guid a, Guid b, bool compareRegistry = false, bool compareVersion = false, bool compareKind = false)
        {
            if (a == default) return b == default;

            //Use the hash code if exact
            return compareRegistry && compareVersion && compareKind
                ? a.GetHashCode() == b.GetHashCode()
                : CompareUL(a.ToByteArray(), b.ToByteArray(), compareRegistry, compareVersion, compareKind);
        }

        private static bool CompareUL(byte[] aBytes, byte[] bBytes, bool compareRegistry = false, bool compareVersion = false, bool compareKind = false)
        {
            if (aBytes is null) return bBytes is null;

            //Use the hash code if exact
            if (compareRegistry && compareVersion && compareKind) return aBytes.GetHashCode() == bBytes.GetHashCode();

            if (BitConverter.ToInt32(aBytes, 0) == BitConverter.ToInt32(bBytes, 0))
            {
                //Registry Designator  = byte 0x05 usually can be different in some cases
                if (compareRegistry ? BitConverter.ToInt16(aBytes, 4) == BitConverter.ToInt16(bBytes, 4) : aBytes[4] == bBytes[4])
                {
                    //MXF decoders shall ignore the version number byte (i.e. byte 7) when determining if a KLV key is the Fill item key.
                    if (compareVersion ? BitConverter.ToInt16(aBytes, 7) == BitConverter.ToInt16(bBytes, 7) : aBytes[7] == bBytes[7])
                    {
                        //9 - 12 should also match
                        if (false == compareKind) return BitConverter.ToInt32(aBytes, 8) == BitConverter.ToInt32(bBytes, 8);

                        //kind usuaully is status
                        return BitConverter.ToInt32(aBytes, 12) == BitConverter.ToInt32(bBytes, 12);
                    }
                }
            }

            //Not a match
            return false;
        }

        public static string ToTextualConvention(byte[] identifier, int offset = 0)
        {
            if (identifier is null) return Media.Common.Extensions.String.StringExtensions.UnknownString;


            Guid id = offset > 0 || identifier.Length > 16 ? new Guid(identifier.Skip(offset).Take(IdentifierSize).ToArray()) : new Guid(identifier);


            //Reduces code but defeats lookup
            //foreach (var kvp in IdentifierLookup) if (CompareUL(kvp.Key.ToByteArray(), identifier, false, false, false)) return kvp.Value;


            //If not an exact match
            if (false == IdentifierLookup.TryGetValue(id, out string result))
            {
                //Attempt by generic compare
                if (CompareUL(UniversalLabel.PartitionPack.ToByteArray(), identifier, false, false, false)) return "PartitionPack"; //Last 4 bytes is version and open or closed
                if (CompareUL(UniversalLabel.OperationalPattern.ToByteArray(), identifier, false, false, false)) return "OperationalPattern";
                if (CompareUL(UniversalLabel.EssenceElement.ToByteArray(), identifier, false, false, false)) return "EssenceElement";
                if (CompareUL(UniversalLabel.PartitionMetadata.ToByteArray(), identifier, false, false, false)) return "PartitionMetadata";
                if (CompareUL(UniversalLabel.StructuralMetadata.ToByteArray(), identifier, false, false, false)) return "StructuralMetadata";
                if (CompareUL(UniversalLabel.DataDefinitionVideo.ToByteArray(), identifier, true, true, true)) return "DataDefinitionVideo";
                if (CompareUL(UniversalLabel.DataDefinitionAudio.ToByteArray(), identifier, true, true, true)) return "DataDefinitionAudio";

                //Unknown
                result = Media.Common.Extensions.String.StringExtensions.UnknownString;
            }

            return result;
        }

        public override string ToTextualConvention(Container.Node node)
        {
            return node.Master.Equals(this) ? MxfReader.ToTextualConvention(node.Identifier) : base.ToTextualConvention(node);
        }


        //ToFourCharacterCode(Guid universalLabel)

        //Possibly seperate Read and Decode logic?

        /// <summary>
        /// Decodes the ASN.1 BER Length from the given packet at the given position.
        /// NOTES that since definite legth is forced this is DER...
        /// </summary>
        public static long DecodeVariableLength(System.IO.Stream stream, out int read)
        {
            read = 0;

            long length = stream.ReadByte();

            ++read;

            if (length >= MultiByteLength)
            {
                length &= ~MultiByteLength;

                if (length is 0) throw new InvalidOperationException("BER32 Indefinite Length Not Supported. Use DER Form.");

                if (length > 8) throw new InvalidOperationException("BER32 Lengths larger than 8 are Not Supported.");

                int end = (int)(length & sbyte.MaxValue);

                //Value is 0
                length = 0;

                //While there are bytes in the value
                while (end-- > 0)
                {
                    length <<= 8;
                    length |= (byte)(stream.ReadByte());
                    ++read;
                }

                if (length < 0) throw new InvalidOperationException("BER32 Lengths > 2^63 are not Supported.");
            }

            return length;
        }

        /// <summary>
        /// Holds a cache of all Fields in the Identifiers static type
        /// </summary>
        private static readonly Dictionary<Guid, string> IdentifierLookup;

        static MxfReader()
        {
            IdentifierLookup = [];

            foreach (var fieldInfo in typeof(UniversalLabel).GetFields()) IdentifierLookup.Add((Guid)fieldInfo.GetValue(null), fieldInfo.Name);
        }

        #endregion

        public MxfReader(string filename, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(filename, access) { }

        public MxfReader(Uri source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public MxfReader(System.IO.FileStream source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public MxfReader(Uri uri, System.IO.Stream source, int bufferSize = 8192) : base(uri, source, null, bufferSize, true) { }

        private int? m_RunInSize, m_MajorVersion, m_MinorVersion, m_IndexByteCount, m_KagSize;
        private Guid? m_OperationalPattern;

        public Version HeaderVersion
        {
            get
            {
                if (false == m_MajorVersion.HasValue) ParseHeader();
                return new Version(m_MajorVersion.Value, m_MinorVersion.Value);
            }
        }

        public int AlignmentGridByteSize
        {
            get
            {
                if (false == m_KagSize.HasValue) ParseHeader();
                return m_KagSize.Value;
            }
        }

        public int IndexByteCount
        {
            get
            {
                if (false == m_IndexByteCount.HasValue) ParseHeader();
                return m_IndexByteCount.Value;
            }
        }

        public bool HasIndex { get { return IndexByteCount > 0; } }

        //6.5 Run-In Sequence
        private void ReadRunIn()
        {
            if (false == m_RunInSize.HasValue)
            {
                /*
                MXF decoders shall ignore the Run-In sequence and parse the data until either the first 11 bytes of the
               Partition Pack label have been located or the maximum Run-In length has been exceeded.
               The Run-In sequence shall be less than 65536 bytes long and shall not contain the first 11 bytes of the
               Partition Pack label.
               Note: The maximum length of the run-in prevents a decoder from searching through an excessively large non-MXF file if
               incorrectly applied to an MXF decoder.
               The default Run-in sequence shall have a length of zero.
               MXF encoders may insert any necessary Run-In sequence provided it conforms to the above provisions, and
               any provisions of the respective specialized Operational Pattern specification. 
                */
                int b = 0;

                while (Remaining > 0 && Position <= ushort.MaxValue)
                {
                    b = ReadByte();

                    if (b == -1) break;

                    switch (b)
                    {
                        case 0x06:
                            {
                                b = ReadByte();

                                if (b == -1) break;

                                if (b != 0x0e) continue;

                                Position -= 2;

                                goto EndWhile;
                            }
                        default: continue;
                    }
                }

                EndWhile:
                m_RunInSize = (int)Position;
            }
        }

        public bool HasRunIn
        {
            get
            {
                return RunInSize > 0;
            }
        }

        public int RunInSize
        {
            get
            {
                if (false == m_RunInSize.HasValue) ReadRunIn();
                return m_RunInSize.Value;
            }
        }

        public Guid OperationalPattern
        {
            get
            {
                if (false == m_OperationalPattern.HasValue) ParseHeader();
                return m_OperationalPattern.Value;
            }
        }

        //Operational Pattern Definition - Item Complexity
        public OperationalPatternItemComplexity ItemComplexity
        {
            get
            {
                if (false == m_OperationalPattern.HasValue) ParseHeader();
                return (OperationalPatternItemComplexity)m_OperationalPattern.Value.ToByteArray()[13];
            }
        }

        //Depends on byte 13
        //public OperationalPatternPackageComplexity PackageComplexity
        //{
        //    get
        //    {
        //        if (!m_OperationalPattern.HasValue) ParseHeader();
        //        return (OperationalPatternPackageComplexity)m_OperationalPattern.Value.ToByteArray()[14];
        //    }
        //}

        private void ParseHeader()
        {
            long position = Position;

            using (var headerPartition = Root)
            {

                if (headerPartition is null) return;

                int offset = 0;

                m_MajorVersion = Common.Binary.Read16(headerPartition.Data, offset, Common.Binary.IsLittleEndian);

                m_MinorVersion = Common.Binary.Read16(headerPartition.Data, offset + 2, Common.Binary.IsLittleEndian);

                offset += 4;

                m_KagSize = Common.Binary.Read32(headerPartition.Data, offset, Common.Binary.IsLittleEndian);

                offset += 4;

                long thisPartition = Common.Binary.Read64(headerPartition.Data, offset, Common.Binary.IsLittleEndian),
                    previousPartition = Common.Binary.Read64(headerPartition.Data, offset + 8, Common.Binary.IsLittleEndian),
                    footerPartitionOffset = Common.Binary.Read64(headerPartition.Data, offset + 16, Common.Binary.IsLittleEndian),
                    headerByteCount = Common.Binary.Read64(headerPartition.Data, offset + 32, Common.Binary.IsLittleEndian);

                m_IndexByteCount = (int)Common.Binary.Read64(headerPartition.Data, offset + 40, Common.Binary.IsLittleEndian);

                //Header should be first partition and have no previous 
                if (thisPartition != 0 || previousPartition != 0) throw new InvalidOperationException("Invalid HeaderParition");

                offset += 40;

                int indexSid = Common.Binary.Read32(headerPartition.Data, offset, Common.Binary.IsLittleEndian);
                offset += 4;

                //Cannot have a index sid when no byte count, dont throw here.
                if (indexSid <= 0 && m_IndexByteCount > 0) m_IndexByteCount = 0;

                //Specfies offset to body (essence container segment)

                long bodyOffset = Common.Binary.Read64(headerPartition.Data, offset, Common.Binary.IsLittleEndian);
                offset += 8;

                int bodySid = Common.Binary.Read32(headerPartition.Data, offset, Common.Binary.IsLittleEndian);
                offset += 4;

                m_OperationalPattern = new Guid(headerPartition.Data.Skip(offset).Take(16).ToArray());

                offset += 16;

                //int batchLen = Common.Binary.Read32(headerPartition.Raw, offset, Common.Binary.IsLittleEndian);

                //offset += 4;

                //Sometimes indicates Essence type e.g. Mpeg2

                //List<Guid> batches = new List<Guid>();

                //if (batchLen > 0)
                //{
                //    int itemLen = Common.Binary.Read32(headerPartition.Raw, offset, Common.Binary.IsLittleEndian);
                //    offset += 4;

                //    for (int i = 0; i < batchLen; ++i)
                //    {
                //        batches.Add(new Guid(headerPartition.Raw.Skip(offset).Take(itemLen).ToArray()));
                //        offset += itemLen;
                //    }
                //}

                PartitionStatus status = (PartitionStatus)Root.Identifier[14];

                //Parse footer if status is not Complete and footer is present.
                if ((status == PartitionStatus.OpenAndIncomplete || status == PartitionStatus.ClosedAndIncomplete) && footerPartitionOffset > 0)
                {
                    Position = footerPartitionOffset;

                    using (var footer = ReadNext())
                    {

                        status = (PartitionStatus)footer.Identifier[14];

                        //Don't parse an incomplete footer
                        if (status is PartitionStatus.OpenAndIncomplete or PartitionStatus.ClosedAndIncomplete) return;

                        offset = 0;

                        m_MajorVersion = Common.Binary.Read16(footer.Data, offset, Common.Binary.IsLittleEndian);

                        m_MinorVersion = Common.Binary.Read16(footer.Data, offset + 2, Common.Binary.IsLittleEndian);

                        offset += 4;

                        m_KagSize = Common.Binary.Read32(footer.Data, offset, Common.Binary.IsLittleEndian);

                        offset += 4;

                        thisPartition = Common.Binary.Read64(footer.Data, offset, Common.Binary.IsLittleEndian);
                        previousPartition = Common.Binary.Read64(footer.Data, offset + 8, Common.Binary.IsLittleEndian);
                        footerPartitionOffset = Common.Binary.Read64(footer.Data, offset + 16, Common.Binary.IsLittleEndian);
                        headerByteCount = Common.Binary.Read64(footer.Data, offset + 32, Common.Binary.IsLittleEndian);

                        //this partition should equal Footer
                        if (thisPartition != footerPartitionOffset) throw new InvalidOperationException("Invalid FooterParition");

                        m_IndexByteCount = (int)Common.Binary.Read64(footer.Data, offset + 40, Common.Binary.IsLittleEndian);
                        offset += 40;

                        indexSid = Common.Binary.Read32(footer.Data, offset, Common.Binary.IsLittleEndian);
                        offset += 4;

                        bodyOffset = Common.Binary.Read64(footer.Data, offset, Common.Binary.IsLittleEndian);
                        offset += 8;

                        bodySid = Common.Binary.Read32(footer.Data, offset, Common.Binary.IsLittleEndian);
                        offset += 4;

                        if (bodyOffset != 0 || bodySid != 0) throw new InvalidOperationException("Invalid FooterPartition");

                        m_OperationalPattern = new Guid(footer.Data.Skip(offset).Take(16).ToArray());

                        offset += 16;

                        //Some times defines the codecs in use...

                        //batchLen = Common.Binary.Read32(headerPartition.Raw, offset, Common.Binary.IsLittleEndian);

                        //offset += 4;

                        //List<Guid> batches = new List<Guid>();

                        //if (batchLen > 0)
                        //{
                        //    int itemLen = Common.Binary.Read32(headerPartition.Raw, offset, Common.Binary.IsLittleEndian);
                        //    offset += 4;

                        //    for (int i = 0; i < batchLen; ++i)
                        //    {
                        //        batches.Add(new Guid(headerPartition.Raw.Skip(offset).Take(itemLen).ToArray()));
                        //        offset += itemLen;
                        //    }
                        //}
                    }
                }
            }

            Position = position;
        }

        private DateTime? m_PrefaceLastModifiedDate;
        private short? m_PrefaceVersion;

        public short PrefaceVersion
        {
            get
            {
                if (false == m_PrefaceVersion.HasValue) ParsePreface();
                return m_PrefaceVersion ?? HeaderVersion.MinorRevision;
            }
        }

        public DateTime PrefaceLastModifiedDate
        {
            get
            {
                if (false == m_PrefaceLastModifiedDate.HasValue) ParsePreface();
                return m_PrefaceLastModifiedDate ?? Modified;
            }
        }

        private void ParsePreface()
        {
            using (var preface = ReadObject(UniversalLabel.Preface, true, Root.DataOffset + Root.DataSize))
            {
                if (preface is null) return;

                int offset = 0, lenth = (int)(preface.DataSize);

                while (offset < lenth)
                {

                    short tag = Common.Binary.Read16(preface.Data, offset, Common.Binary.IsLittleEndian),
                        tagLen = Common.Binary.Read16(preface.Data, offset + 2, Common.Binary.IsLittleEndian);

                    offset += 4;

                    switch (tag)
                    {
                        case 0x3b02:
                            {
                                m_PrefaceLastModifiedDate = new DateTime(Common.Binary.ReadU16(preface.Data, offset, Common.Binary.IsLittleEndian),
                                    preface.Data[offset + 2],
                                    preface.Data[offset + 3],
                                    preface.Data[offset + 4],
                                    preface.Data[offset + 5],
                                    preface.Data[offset + 6],
                                    preface.Data[offset + 7],
                                    DateTimeKind.Utc);
                                goto default;
                            }
                        case 0x3b05:
                            {
                                //Should be 259
                                //Also found 258 and lower
                                //Sometimes found something higher....
                                m_PrefaceVersion = (short)Common.Binary.ReadInteger(preface.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                goto default;
                            }
                        #region Unused
                        //case 0x3b03:
                        //    {
                        //        //ContentStorage
                        //        //ul
                        //        goto default;
                        //    }
                        //case 0x3b06:
                        //    {
                        //        //Identifications 
                        //        //Array of StrongRef
                        //        //8 + 16n

                        //        //Read len (8)
                        //        //Read 16 * len
                        //        goto default;
                        //    }
                        //case 0x3b07:
                        //    {
                        //        m_ObjectModelVersion = (int)Common.Binary.ReadInteger(preface.Raw, offset, tagLen, Common.Binary.IsLittleEndian);
                        //        goto default;
                        //    }
                        //case 0x3b08:
                        //    {
                        //        m_PrimaryPackage = new Guid(preface.Raw.Skip(offset).Take(tagLen).ToArray());
                        //        goto default;
                        //    }
                        //case 0x3b09:
                        //    {
                        //        //Matches m_OperationalPattern
                        //        m_PrefaceOperationalPattern = new Guid(preface.Raw.Skip(offset).Take(tagLen).ToArray());
                        //        goto default;
                        //    }
                        //case 0x3b0a: //(List of all tracks?)
                        //    {
                        //        //EssenceContainers
                        //        // This Batch shall contain all values that appear in the Essence Container Property of all File Descriptors
                        //        //(including the Multiple Descriptor) of all Top-Level File Packages in the File that describe internal Essence. 
                        //        //Batch of UL
                        //        //Read len (8)
                        //        //Read 16 * len
                        //        goto default;
                        //    }
                        #endregion
                        default: offset += tagLen; continue;
                    }

                }

                if (false == m_PrefaceLastModifiedDate.HasValue && false == m_PrefaceVersion.HasValue) throw new InvalidOperationException("Invalid Preface Object");
            }
        }

        private string m_CompanyName, m_ProductName, m_ProductVersion, m_Platform;

        public string CompanyName
        {
            get
            {
                if (m_CompanyName is null) ParseIdentification();
                return m_CompanyName ?? string.Empty;
            }
        }

        public string ProductName
        {
            get
            {
                if (m_ProductName is null) ParseIdentification();
                return m_ProductName ?? string.Empty;
            }
        }

        public string ProductVersion
        {
            get
            {
                if (m_ProductVersion is null) ParseIdentification();
                return m_ProductVersion ?? string.Empty;
            }
        }

        public string Platform
        {
            get
            {
                if (m_Platform is null) ParseIdentification();
                return m_Platform ?? string.Empty;
            }
        }

        private Guid? m_ProductUID;

        public Guid ProductUID
        {
            get
            {
                if (false == m_ProductUID.HasValue) ParseIdentification();
                return m_ProductUID ?? Guid.Empty;
            }
        }

        private DateTime? m_IdentificationModificationDate;

        public DateTime IdentificationModificationDate
        {
            get
            {
                if (false == m_IdentificationModificationDate.HasValue) ParseIdentification();
                return m_IdentificationModificationDate ?? FileLastWriteTimeUtc;
            }
        }

        private void ParseIdentification()
        {
            using (var identification = ReadObject(UniversalLabel.Identification, true, Root.DataOffset + Root.DataSize))
            {
                if (identification is null) return;
                int offset = 0, lenth = (int)(identification.DataSize);

                while (offset < lenth)
                {

                    short tag = Common.Binary.Read16(identification.Data, offset, Common.Binary.IsLittleEndian),
                        tagLen = Common.Binary.Read16(identification.Data, offset + 2, Common.Binary.IsLittleEndian);

                    offset += 4;

                    switch (tag)
                    {
                        case 0x3c01:
                            {
                                m_CompanyName = Encoding.BigEndianUnicode.GetString(identification.Data.Array, offset, tagLen);
                                goto default;
                            }
                        case 0x3c02:
                            {
                                m_ProductName = Encoding.BigEndianUnicode.GetString(identification.Data.Array, offset, tagLen);
                                goto default;
                            }
                        //case 0x3c03:
                        //    {
                        //        //Product Version 10 byte type
                        //Suprisingly NOT REQUIRED
                        //        //ALL 2 bytes

                        //        //Major
                        //        //Minor
                        //        //Patch
                        //        //Build
                        //        //Release

                        //        goto default;
                        //    }
                        case 0x3c04:
                            {
                                m_ProductVersion = Encoding.BigEndianUnicode.GetString(identification.Data.Array, offset, tagLen);
                                goto default;
                            }
                        case 0x3c05:
                            {
                                m_ProductUID = new Guid(identification.Data.Skip(offset).Take(tagLen).ToArray());
                                goto default;
                            }
                        case 0x3c06:
                            {
                                m_IdentificationModificationDate = new DateTime(Common.Binary.ReadU16(identification.Data, offset, Common.Binary.IsLittleEndian),
                                   identification.Data[offset + 2],
                                   identification.Data[offset + 3],
                                   identification.Data[offset + 4],
                                   identification.Data[offset + 5],
                                   identification.Data[offset + 6],
                                   identification.Data[offset + 7],
                                   DateTimeKind.Utc);
                                goto default;
                            }
                        case 0x3c08:
                            {
                                m_Platform = Encoding.BigEndianUnicode.GetString(identification.Data.Array, offset, tagLen);
                                goto default;
                            }
                        default: offset += tagLen; continue;
                    }

                }

                m_Platform ??= string.Empty;

                if (false == m_IdentificationModificationDate.HasValue
                    //Important?
                    || false == m_ProductUID.HasValue
                    || null == m_ProductName || null == m_ProductVersion || null == m_CompanyName) throw new InvalidOperationException("Invalid Preface Object");

            }
        }

        private DateTime? m_MaterialCreationDate, m_MaterialModifiedDate;

        public DateTime Created
        {
            get
            {
                if (false == m_MaterialCreationDate.HasValue) ParseMaterialPackage();
                return m_MaterialCreationDate ?? FileCreationTimeUtc;
            }
        }

        public DateTime Modified
        {
            get
            {
                if (false == m_MaterialModifiedDate.HasValue) ParseMaterialPackage();
                return m_MaterialModifiedDate ?? FileLastWriteTimeUtc;
            }
        }

        private void ParseMaterialPackage()
        {
            using (var materialPackage = ReadObject(UniversalLabel.MaterialPackage, true, Root.DataOffset + Root.DataSize))
            {

                if (materialPackage is null) return;

                int offset = 0, lenth = (int)(materialPackage.DataSize);

                while (offset < lenth)
                {

                    short tag = Common.Binary.Read16(materialPackage.Data, offset, Common.Binary.IsLittleEndian),
                        tagLen = Common.Binary.Read16(materialPackage.Data, offset + 2, Common.Binary.IsLittleEndian);

                    offset += 4;

                    switch (tag)
                    {
                        //case 0x4403:
                        //    {
                        //        //Array of StrongRef (Tracks)
                        //        // Read 4 byte count of list
                        //        //TrackId List (16 * count)
                        //        //Standard indicated 8 but seems 32 is right?

                        //        long count = Common.Binary.Read32(preface.Raw, offset, Common.Binary.IsLittleEndian);

                        //        int localOffset = offset + 4;

                        //        List<Guid> idens = new List<Guid>();

                        //        for (int i = 0; i < count; ++i)
                        //        {
                        //            idens.Add(new Guid(preface.Raw.Skip(localOffset).Take(IdentifierSize).ToArray()));
                        //            localOffset += IdentifierSize;
                        //        }

                        //        goto default;
                        //    }
                        //case 0x4402:
                        //    {
                        //        m_MaterialName = Encoding.BigEndianUnicode.GetString(materialPackage.Raw, offset, tagLen);
                        //        goto default;
                        //    }
                        case 0x4404:
                            {
                                m_MaterialModifiedDate = new DateTime(Common.Binary.ReadU16(materialPackage.Data, offset, Common.Binary.IsLittleEndian),
                                   materialPackage.Data[offset + 2],
                                   materialPackage.Data[offset + 3],
                                   materialPackage.Data[offset + 4],
                                   materialPackage.Data[offset + 5],
                                   materialPackage.Data[offset + 6],
                                   materialPackage.Data[offset + 7],
                                   DateTimeKind.Utc);
                                goto default;
                            }
                        case 0x4405:
                            {
                                m_MaterialCreationDate = new DateTime(Common.Binary.ReadU16(materialPackage.Data, offset, Common.Binary.IsLittleEndian),
                                   materialPackage.Data[offset + 2],
                                   materialPackage.Data[offset + 3],
                                   materialPackage.Data[offset + 4],
                                   materialPackage.Data[offset + 5],
                                   materialPackage.Data[offset + 6],
                                   materialPackage.Data[offset + 7],
                                   DateTimeKind.Utc);
                                goto default;
                            }
                        default: offset += tagLen; continue;
                    }

                }

                if (false == m_MaterialModifiedDate.HasValue || false == m_MaterialCreationDate.HasValue) throw new InvalidOperationException("Invalid MaterialPackage");
            }
        }

        //ParseContentStorage? 
        //Contains an 1901 tag with Packages and 1902 tag with EssenceContainerData

        #region From FileInfo

        //From FileInfo

        public DateTime FileCreationTimeUtc
        {
            get { return FileInfo.CreationTimeUtc; }
        }

        public DateTime FileLastWriteTimeUtc
        {
            get { return FileInfo.LastWriteTimeUtc; }
        }

        #endregion

        public IEnumerable<Node> ReadObjects(long offset = 0, bool exact = false, params Guid[] names) { return Resolve(offset, Length - offset, exact, exact, exact, names); }

        public Node ReadObject(Guid name, bool exact = false, long offset = 0)
        {
            long positionStart = Position;

            Node result = Resolve(offset, Length - offset, exact, exact, exact, name).FirstOrDefault();

            Position = positionStart;

            return result;
        }

        //In the next update all readers should support the count when reading
        //They should also have a way to determine what files types they support and their corresponding Mime information


        /// <summary>
        /// Matches any Key Length Value Pairs using the given parameters
        /// </summary>
        /// <param name="offset">The offset to start the search</param>
        /// <param name="count">The amount of bytes to read</param>
        /// <param name="ignoreRegistry">Determines if the registry byte of the Key's Universal Label should match</param>
        /// <param name="ignoreVersion">Determines if the verison byte of the Key's Universal Label should match</param>
        /// <param name="ignoreType">Determines if the last four byte's of the Key's Universal Label should match</param>
        /// <param name="names">The Universal Labels to search for</param>
        /// <returns>The <see cref="Node"/>'s which match the given criteria</returns>
        public IEnumerable<Node> Resolve(long offset, long count, bool ignoreRegistry, bool ignoreVersion, bool ignoreType, params Guid[] names)
        {
            if (count <= 0) yield break;

            long position = Position;

            Position = offset;

            bool exact = (ignoreRegistry == ignoreVersion == ignoreType);

            foreach (var mxfObject in this)
            {
                Guid objectId = new(mxfObject.Identifier);

                if (names is null || names.Count() is 0 || (exact ? names.Contains(objectId) : names.Any(n => CompareUL(n, objectId, ignoreRegistry, ignoreVersion, ignoreType))))
                    yield return mxfObject;

                count -= mxfObject.TotalSize;

                if (count <= 0) break;
            }

            Position = position;

            yield break;
        }

        public Node ReadNext()
        {
            if (Remaining < MinimumSize) return null;

            byte[] identifier = new byte[IdentifierSize];

            Read(identifier, 0, IdentifierSize);


            long length = DecodeVariableLength(this, out int sizeLength);

            return sizeLength < MinimumSizeLength
                ? throw new InvalidOperationException("Cannot Decode Length")
                : new Node(this, identifier, sizeLength, Position, length, length <= Remaining);
        }

        public override IEnumerator<Node> GetEnumerator()
        {
            while (Remaining > MinimumSize)
            {
                Node next = ReadNext();

                if (next is null) yield break;

                yield return next;

                Skip(next.DataSize);
            }
        }

        private List<Track> m_Tracks;

        /// <summary>
        /// Provides a lookup of a (Related/Linked)TrackId to a GenericDescriptor
        /// </summary>
        private Common.Collections.Generic.ConcurrentThesaurus<int, Node> m_TrackDescriptors;

        /// <summary>
        /// Obtains information which describes all tracks in the container
        /// </summary>
        private void ParseGenericDescriptors()
        {
            if (m_TrackDescriptors is not null) return;

            int offsetStart = 0, trackId = -1;

            using (var root = Root) offsetStart = (int)(root.DataOffset + root.DataSize);

            //Must assoicate a descriptor to a track so the properties can be read.

            //To be more efficient all tags could be parsed and converted to a Generic.Dictionary<int, byte[]>
            //Then rather than Node a Generic.Dictionary<int, byte[]> would be available for quick retrival.
            //This would also stop the parsing a second time in the logic below.

            //Could also make virtual Nodes with each tag but would increase IO

            //Create a lookup to asscioate a descriptor to the node
            m_TrackDescriptors = [];

            //Iterate the GenericDescriptors in the file parsing for trackId.
            foreach (var descriptor in ReadObjects(offsetStart, false, UniversalLabel.GenericDescriptor).ToArray())
            {
                int offset = 0, lenth = (int)descriptor.DataSize;

                //Don't reset the trackId because apparently it means that its the from the lastId encountered...
                //trackId = -1;

                //Dont part top level metaData for tracks (Use CompareUL)?
                if (descriptor.Identifier.SequenceEqual(UniversalLabel.Preface.ToByteArray())) continue;

                if (descriptor.Identifier.SequenceEqual(UniversalLabel.Identification.ToByteArray())) continue;

                if (descriptor.Identifier.SequenceEqual(UniversalLabel.ContentStorage.ToByteArray())) continue;

                if (descriptor.Identifier.SequenceEqual(UniversalLabel.MaterialPackage.ToByteArray())) continue;

                //Iterate tags in descriptor
                while (offset < lenth)
                {
                    //Maybe should check Registry of descriptor.Identifier to determine if use Ber Length or otherwise...

                    short tag = Common.Binary.Read16(descriptor.Data, offset, Common.Binary.IsLittleEndian),
                        tagLen = Common.Binary.Read16(descriptor.Data, offset + 2, Common.Binary.IsLittleEndian);

                    offset += 4;

                    switch (tag)
                    {

                        case 0x3006:// Linked Track ID
                        case 0x4801:// Track ID
                            {
                                trackId = (int)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                //Only parse the trackId
                                offset = lenth;
                                goto default;
                            }
                        default: offset += tagLen; continue;
                    }
                }

                //Should keep parsed values to increase parsing time and decrease memory usage and IO.

                //Add to the lookup by trackNumber if allowed
                if (trackId > 0) m_TrackDescriptors.Add(trackId, descriptor);
            }
        }

        public override IEnumerable<Track> GetTracks()
        {

            if (m_Tracks is not null)
            {
                foreach (Track track in m_Tracks) yield return track;
                yield break;
            }

            var tracks = new List<Track>();

            long position = Position;

            DateTime trackCreated = Created, trackModified = Modified;

            Node timelineTrackObject = null;

            string trackName = string.Empty;

            //Essence?
            byte[] codecIndication = Media.Common.MemorySegment.EmptyBytes;

            double startTime = 0, duration = 0, editRate = 0, rate = 0;

            Sdp.MediaType mediaType = Sdp.MediaType.unknown;

            int trackId = 0, trackNumber = 0, width = 0, height = 0, lastTrackNumber = -1;

            byte channels = 0, bitDepth = 0;

            //Obtain track information from meta data
            ParseGenericDescriptors();

            //Iterate each descriptor related to a track and parse it
            foreach (var descriptorKey in m_TrackDescriptors.Keys)
            {
                //Iterate descriptors realted to the track
                foreach (var descriptor in m_TrackDescriptors[descriptorKey])
                {
                    //If the descriptor is the Timeline Track store a reference (CompareUL ?)
                    if (descriptor.Identifier.SequenceEqual(UniversalLabel.TimelineTrack.ToByteArray())) timelineTrackObject = descriptor;

                    int offset = 0, lenth = (int)descriptor.DataSize;

                    //Iterate tags and parse properties
                    while (offset < lenth)
                    {
                        //Maybe should check Registry of descriptor.Identifier to determine if use Der Length or otherwise...
                        short tag = Common.Binary.Read16(descriptor.Data, offset, Common.Binary.IsLittleEndian),
                            tagLen = Common.Binary.Read16(descriptor.Data, offset + 2, Common.Binary.IsLittleEndian);

                        //Move offset for bytes consumed
                        offset += 4;

                        switch (tag)
                        {
                            case 0x3006:// Linked Track ID
                            case 0x4801:// Track ID
                                {
                                    trackId = (int)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            case 0x4804: //Track Number
                                {

                                    /*
                                     The value of the Track Number should be set to zero in all Material Package and Lower-Level Source Package
                                        Essence Tracks and in all Descriptive Metadata Tracks.
                                        Note: Some MXF encoders create files that contain non-zero Track Number Properties in Material Package Essence Tracks.
                                        Non-zero values of the Track Number Property in Essence Tracks of Material or Lower-Level Source Packages,
                                        and non-zero values of the Track Number Property in Descriptive Metadata Tracks should be treated as Dark
                                        Metadata.
                                     */
                                    trackNumber = (int)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            case 0x4405: //Package Creation Date
                                {
                                    trackCreated = new DateTime(Common.Binary.ReadU16(descriptor.Data, offset, Common.Binary.IsLittleEndian),
                                       descriptor.Data[offset + 2],
                                       descriptor.Data[offset + 3],
                                       descriptor.Data[offset + 4],
                                       descriptor.Data[offset + 5],
                                       descriptor.Data[offset + 6],
                                       descriptor.Data[offset + 7],
                                       DateTimeKind.Utc);
                                    goto default;
                                }
                            case 0x4404: // Package Modified Date
                            case 0x3c06: // Modification Date
                            case 0x3b02: // Last Modified Date
                                {
                                    trackModified = new DateTime(Common.Binary.ReadU16(descriptor.Data, offset, Common.Binary.IsLittleEndian),
                                        descriptor.Data[offset + 2],
                                        descriptor.Data[offset + 3],
                                        descriptor.Data[offset + 4],
                                        descriptor.Data[offset + 5],
                                        descriptor.Data[offset + 6],
                                        descriptor.Data[offset + 7],
                                        DateTimeKind.Utc);
                                    goto default;
                                }
                            case 0x4402: //Generic Package .Name
                            case 0x4802: //Track Name
                                {
                                    //TrackName
                                    trackName = Encoding.BigEndianUnicode.GetString(descriptor.Data.Array, offset, tagLen);
                                    goto default;
                                }
                            case 0x4b01: //Edit Rate (in hertz)
                                {
                                    editRate = Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            //case 0x1501: //Start Timecode (Position) Starting Timecode (Converted to integer frame count from XX:XX:XX:XX)
                            case 0x1201: //Start Position (Position)
                            case 0x4b02: //Origin (Position)
                                {
                                    startTime = Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            //case 0x3001: //Sample Rate ([RP 210 Specifies the number of addressable elements of essence data per second]
                            case 0x1502: //Rounded Timecode Base (UInt16) (2 byte) (Nearest Integer Frames Per Second)
                                {
                                    rate = (int)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            //case 0x1503: { goto default; } // DropFrame (Boolean 1 byte) Specifies wheater timecode is drop frame, non Drop Frame = 0                            
                            case 0x0201: //Data Definition (Universal Label)
                            case 0x3005: //Codec UL
                                {

                                    //Data Definition
                                    //UL, Specifies the data type of this set
                                    codecIndication = descriptor.Data.Skip(offset).Take(tagLen).ToArray();

                                    if (mediaType == Sdp.MediaType.unknown)
                                    {
                                        if (CompareUL(codecIndication, Guid.Empty.ToByteArray(), true, true, true)) mediaType = Sdp.MediaType.data;
                                        else if (CompareUL(codecIndication, UniversalLabel.DataDefinitionVideo.ToByteArray(), true, true, true)
                                            ||
                                            CompareUL(codecIndication, UniversalLabel.DataDefinitionVideoLegacy.ToByteArray(), true, true, true))
                                        {
                                            mediaType = Sdp.MediaType.video;
                                        }
                                        else if (CompareUL(codecIndication, UniversalLabel.DataDefinitionAudio.ToByteArray(), true, true, true)
                                            ||
                                            CompareUL(codecIndication, UniversalLabel.DataDefinitionAudioLegacy.ToByteArray(), true, true, true))
                                        {
                                            mediaType = Sdp.MediaType.audio;
                                        }
                                    }

                                    goto default;
                                }
                            case 0x3002: //Container Duration (measured in Edit Units)
                            case 0x0202: //Duration (in units of Edit Rate)
                                {
                                    duration = Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            case 0x3201: // Picture Essence Coding (UniversalLabel)
                                {
                                    mediaType = Sdp.MediaType.video;
                                    codecIndication = descriptor.Data.Skip(offset).Take(tagLen).ToArray();
                                    goto default;
                                }
                            case 0x3202: //Stored Height
                            case 0x3204: //Sampled Height
                                {
                                    mediaType = Sdp.MediaType.video;
                                    height = (int)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            case 0x3203: //Stored Width
                            case 0x3205: //Sampled With
                                {
                                    mediaType = Sdp.MediaType.video;
                                    width = (int)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }

                            case 0x3301: //Component Depth (Video)
                                {
                                    mediaType = Sdp.MediaType.video;
                                    bitDepth = (byte)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            //case 0x3303:
                            //    {
                            //        //Color Sitting - 0 Specifies how to compute subsampled color difference values
                            //        //Not sure if this is correct
                            //        bitDepth *= trackCodecObject.Raw[offset];
                            //        goto default;
                            //    }
                            //case 0x3302:
                            //    {
                            //        //Horizontal Sub Sampling
                            //        bitDepth *= (byte)Common.Binary.ReadInteger(trackCodecObject.Raw, offset, tagLen, Common.Binary.IsLittleEndian);
                            //        goto default;
                            //    }
                            //case 0x3308:
                            //    {
                            //        //Vertical Sub Sampling
                            //        bitDepth *= (byte)Common.Binary.ReadInteger(trackCodecObject.Raw, offset, tagLen, Common.Binary.IsLittleEndian);
                            //        goto default;
                            //    }
                            //case 0x3309:
                            //    {
                            //        //Number of bits per alpha sub sample?
                            //        int AlphaSubSampl = (int)Common.Binary.ReadInteger(trackCodecObject.Raw, offset, tagLen, Common.Binary.IsLittleEndian);
                            //        goto default;
                            //    }
                            case 0x3401:
                                {
                                    mediaType = Sdp.MediaType.video;
                                    //reset bitDept to calculate from layout
                                    bitDepth = 0;
                                    int localOffset = offset;
                                    for (int i = 0; i < 16; ++i)
                                    {
                                        //Increase index
                                        ++i;

                                        //Component [ARGB, argb, F, YCBR]
                                        if (descriptor.Data[++localOffset] is 0) break;

                                        //Bits per component
                                        bitDepth += descriptor.Data[localOffset++];
                                    }
                                    goto default;
                                }
                            case 0x3d06://SoundEssenceCompression
                                {
                                    mediaType = Sdp.MediaType.audio;
                                    codecIndication = descriptor.Data.Skip(offset).Take(tagLen).ToArray();
                                    goto default;
                                }
                            case 0x3d07://ChannelCount
                                {
                                    mediaType = Sdp.MediaType.audio;
                                    channels = (byte)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            case 0x3d03:
                                {
                                    //Audio Sampling Rate (8 bytes but the Distinguised Value is either 0 or 1)
                                    mediaType = Sdp.MediaType.audio;
                                    rate = Common.Binary.ReadU32(descriptor.Data, offset, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            case 0x3d01:  //Quantization bits (Audio)
                                {
                                    bitDepth = (byte)Common.Binary.ReadInteger(descriptor.Data, offset, tagLen, Common.Binary.IsLittleEndian);
                                    goto default;
                                }
                            //case 0x4803:
                            //    {
                            //        //Sequence (Defines the Sequence Set)....
                            //        goto default;
                            //    }
                            //case 0x1902: //EssenceContainerData (Batch of Universal Label)
                            //case 0x1901: //Packages (Batch of Universal Label)
                            //    {
                            //        int amount = (int)Common.Binary.ReadInteger(timelineTrackObject.Raw, offset, 4, Common.Binary.IsLittleEndian);

                            //        int localOffset = offset + 4;

                            //        List<Guid> sids = new List<Guid>();

                            //        for (int i = 0; i < amount; ++i)
                            //        {
                            //            sids.Add(new Guid(timelineTrackObject.Raw.Skip(localOffset).Take(16).ToArray()));
                            //            localOffset += 16;
                            //        }

                            //        goto default;
                            //    }
                            default: offset += tagLen; continue;
                        }

                    }
                }

                //Do not yield Timecode Tracks or duplicate entries
                if (trackNumber is 0 || lastTrackNumber == trackNumber) continue;

                //Try to use the trackName if needed and we can
                if (mediaType == Sdp.MediaType.unknown && false == string.IsNullOrWhiteSpace(trackName)) switch (trackName)
                    {
                        case PictureTrack: mediaType = Sdp.MediaType.video; break;
                        case AudioTrack: mediaType = Sdp.MediaType.audio; break;
                        case TimecodeTrack: mediaType = Sdp.MediaType.timing; break;
                        case TextTrack: mediaType = Sdp.MediaType.text; break;
                        case DataTrack: mediaType = Sdp.MediaType.data; break;
                    }

                //Convert codecIndication to 4cc? sometimes is the last 4

                //Get sampleCount if index is available? or count all frames...

                //Guid is EssenceElement
                //MxfReader.UniversalLabel.EssenceElement.ToByteArray()
                int sampleCount = ReadObjects(timelineTrackObject.DataOffset + timelineTrackObject.DataSize, false, new Guid(new byte[] { 0x06, 0x0e, 0x2b, 0x34, 0x01, 0x02, 0x01, 0x01, 0x0d, 0x01, 0x03, 0x01, 
                    //Modified with trackNumber
                    (byte)((trackNumber >> 24) & byte.MaxValue), (byte)((trackNumber >> 16) & byte.MaxValue), (byte)((trackNumber >> 8) & byte.MaxValue), (byte)(trackNumber & byte.MaxValue) })).Count();

                //Do duration calculation in a function and possibly do it above when there is a trackname

                Track created = new(timelineTrackObject, trackName, trackId, trackCreated, trackModified, sampleCount, height, width, TimeSpan.FromSeconds(startTime * editRate),
                    //Duration calculation for Audio
                    (mediaType == Sdp.MediaType.audio ?
                        TimeSpan.FromMilliseconds(duration * rate / Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond)
                        : //Video
                        TimeSpan.FromMilliseconds(duration * (1 / rate) * Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond)),
                    rate, mediaType, codecIndication, channels, bitDepth);

                yield return created;

                tracks.Add(created);

                lastTrackNumber = trackNumber;
            }

            Position = position;

            m_Tracks = tracks;
        }

        public override Common.SegmentStream GetSample(Track track, out TimeSpan duration)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads any "Run In" and Provides either the First Header, Body or Footer (Primer) ParitionPack found in the file.
        /// </summary>
        public override Node Root
        {
            get
            {
                return ReadObject(UniversalLabel.PartitionPack, false, RunInSize);
            }
        }

        /// <summary>
        /// Provides the Index if possible
        /// </summary>
        public override Node TableOfContents
        {
            get
            {
                if (false == HasIndex) return null;
                using (var root = Root) return ReadObject(UniversalLabel.Index, true, root.DataOffset + root.DataSize);
            }
        }
    }
}
