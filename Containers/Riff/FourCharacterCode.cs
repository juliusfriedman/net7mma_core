namespace Media.Containers.Riff;

#region FourCharacterCode

public enum FourCharacterCode
{
    //File Headers
    RIFF = 1179011410,
    RIFX = 1481001298,
    ON2 = 540167759,
    odml = 1819108463,
    //AVI Header
    avih = 1751742049,
    //Extended Header
    dmlh = 1751936356,
    ds64 = 875983716,
    //File Types
    AVI = 541677121,
    AVIX = 1481201217,
    AVI_ = 424236609,
    AVIF = 1179211329,
    ON2f = 1714572879,
    AMV = 542526785,
    WAVE = 1163280727,
    RF64 = 875972178,
    RMID = 1145654610,
    //Types
    LIST = 1414744396,
    hdlr = 1919706216,
    rec = 543384946,
    //Chunks
    JUNK = 1263424842,
    ISMP = 1347244873, //Timecode
    INFO = 1330007625,
    IDIT = 1414087753,
    INAM = 1296125513,
    ISTR = 1381258057,
    ISFT = 1413894985,
    IART = 1414676809,
    IWRI = 1230133065,
    ICMT = 1414349641,
    IGRN = 1314015049,
    ICRD = 1146241865,
    IPRT = 1414680649,
    IFRM = 1297237577,
    ICOP = 1347371849,
    //MovieId
    MID = 541346125,
    TITL = 1280592212,
    COMM = 1296912195,
    GENR = 1380861255,
    PRT1 = 827609680,
    PRT2 = 844386896,
    nctg = 1735680878,  //Nikon Tags
    CASI = 1230192963, //CASIO
    Zora = 1634889562,  //
                        //Stream Chunks
    movi = 1769369453,
    strh = 1752331379,
    strf = 1718776947,
    strl = 1819440243,
    strn = 1852994675,
    strd = 1685222515,
    //Extended Video Properties
    vprp = 1886548086,
    //Sample Chunks
    dc = 1667510000, //DIB (Video)
    db = 1650730000, //DIBCompressed
    wb = 1651970000, //WaveBytes (Audio)
    tx = 2020880000, //Text
    ix = 2020150000, //Index
    pc = 1668290000, //PalChange
                     //Index Chunks
    idx1 = 829973609,
    indx = 2019847785,
    //Stream Types
    iavs = 1937138025, //Interleaved Audio + Video
    vids = 1935960438,
    auds = 1935963489,
    data = 1635017060,
    mids = 1935960429,
    txts = 1937012852,
    //WAVE
    fmt = 544501094,
    // Video Codecs
    MJPG = 1196444237,
}

#endregion