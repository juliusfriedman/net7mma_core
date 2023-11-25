namespace Media.Rtcp.Feedback
{
    public class ReferencePictureSelectionIndicationReport : PayloadSpecificFeedbackReport
    {
        public ReferencePictureSelectionIndicationReport(int version, int padding, int ssrc, int mssrc, byte[] fci)
            : base(version, Media.Rtcp.Feedback.RFC4585.FeedbackControlInformationType.ReferencePictureSelectionIndication, padding, ssrc, mssrc, fci)
        {

        }
    }
}
