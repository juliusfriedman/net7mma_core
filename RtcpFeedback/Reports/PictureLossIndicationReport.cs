﻿namespace Media.Rtcp.Feedback
{
    public class PictureLossIndicationReport : PayloadSpecificFeedbackReport
    {
        public PictureLossIndicationReport(int version, int padding, int ssrc, int mssrc, byte[] fci)
            : base(version, Media.Rtcp.Feedback.RFC4585.FeedbackControlInformationType.PictureLossIndication, padding, ssrc, mssrc, fci)
        {

        }
    }
}
