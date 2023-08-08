# net7mma_core
.Net Core version of the net7mma library

This project contains facilities for working with various protocols and binary formats related to media processing.

You can use the Rtp project to handle sending / recieving Rtp and Rtcp packets or use the RtpClient for processing of the packets automatically.

You can use the RtspServer project to handle creating media and serving it to clients (you can also use it's frame types to handle depacketization of media) [which will eventually be seperated into their own classes so you don't need the server to packetize or depacketize]

You can use the Image project to create Images (save to Bitmap only right now)

You can use the Audio project to create Audio (See examples for saving to a Riff / Wave file)

Various transforms are supported for the Image and Audio buffers, [using vectorization where possible]

You can read version media files using the various Container projects

A lot of the classes are still in development mode so please let me know if you see a feature missing or a gap in the API.

Contibutions welcome!
