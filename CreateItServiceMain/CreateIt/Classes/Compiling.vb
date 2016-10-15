Imports System.Threading
Imports MySql.Data.MySqlClient
Imports MySql.Data
Imports System.IO
Imports System.Speech
Imports System.Speech.Synthesis
Imports SpeechLib
Imports System.Text
Imports System.Drawing.Imaging
Imports CreateIt.neoSpeech
Imports System.Net


Public Class Compiling
    Implements QueueItem

#Region "Variables and Constants and Enum"

    Private intVideoID As Integer

    Private blnCompress As Boolean = True
    Private blnAudio As Boolean = True
    Private blnStream As Boolean = True
    Private blnFrames As Boolean = True
    Private blnMakeHDVideo As Boolean = True

    Private VideoWidth As Integer = 0
    Private VideoHeight As Integer = 0
    Private HResizedWidth As Double
    Private HResizedHeight As Double
    Private VResizedWidth As Double
    Private VResizedHeight As Double
    Private ResizeX As Double
    Private ResizeY As Double
    Private intNarrationDuration As Integer = 0
    Private intVoiceOver As Integer = 0

    Private MusicVolume As Single = 0.5
    Private NarrationVolume As Single = 1.25

    'Private WithEvents video As New Animage.AnimageVideo(VideoWidth, VideoHeight, 30, 85)
    Private WithEvents audio As New EasyAudio.EasyAudio
    Private WithEvents video As Animage.AnimageVideo


    Private SLIDE_DURATION As Integer = 7
    Private VERTICAL_SLIDE_DURATION As Integer = 13

    Const INTRO_SLIDE_DURATION As Integer = 5
    Const CLOSED_SLIDE_DURATION As Integer = 10

    Private FIXED_LOGO_HEIGHT As Integer = 150
    Private FIXED_LOGO_WIDTH As Integer = 150
    Private FIXED_AGENTPHOTO_HEIGHT As Integer = 230
    Private FIXED_AGENTPHOTO_WIDTH As Integer = 165

    Const LOGO_BOUND_WIDTH As Integer = 518
    Const LOGO_BOUND_Height As Integer = 365
    Const ZoomOutPercent As Integer = 25

    'Public WithEvents vox As New SpVoice

    'Private Enum NeoSpeechResultCode
    '    success = 0
    '    invalid_login = -1
    '    account_inactive = -2
    '    account_unauthorized = -3
    '    invalid_or_inactive_login_key = -4
    '    invalid_conversion_number_lookup = -5
    '    content_size_is_too_large_only_for_Basic_subscribers = -6
    '    monthly_allowance_has_been_exceeded_only_for_Basic_subscribers = -7
    '    invalid_TTS_Voice_ID = -10
    '    invalid_TTS_Output_Format_ID = -11
    '    invalid_REST_request = -12
    '    invalid_or_unavailable_TTS_Sample_Rate = -13
    '    invalid_SSML_not_a_valid_XML_document = 1
    '    invalid_SSML_SSML_content_must_begin_with_a_speak_tag = 2
    '    invalid_SSML_lexicon_tag_is_not_supported = 3
    'End Enum
    'Private Enum NeoSpeechStatusCode
    '    queued = 1
    '    processing = 2
    '    awaiting_completion = 3
    '    completed = 4
    '    failed = 5
    'End Enum


    Private Enum Voices
        None = 0
        Kate = 1 'American US
        Bridget = 2 ''British UK
        'Salli = 1
        'Amy = 2
    End Enum

    Private Enum HorizontalPanningEffect
        RightToLeft = 1
        LeftToRight = 2
    End Enum

    Private Enum VerticalPanningEffect
        TopToBottom = 1
        BottomToTop = 2
    End Enum

#End Region

    Public Event ProcessCompleted(ByVal item As QueueItem) Implements QueueItem.ProcessCompleted

    Public Event ProcessNotCompleted(ByVal item As QueueItem, ByVal exception As System.Exception) Implements QueueItem.ProcessNotCompleted

    Public Sub New(ByVal VideoID As Integer)
        intVideoID = VideoID
    End Sub

    Public Sub StartProcess() Implements QueueItem.StartProcess
        Dim trdimages As New Thread(AddressOf GetImages)
        trdimages.Start()
    End Sub

    Private Sub GetImages()
        Dim conImages As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conImages = Database.Connection.Clone
            If Not conImages.State = ConnectionState.Open Then Call conImages.Open()

            Dim ImageName As String = ""
            Dim dtImages As New DataTable

            dtImages = Database.FetchDataTable("SELECT imageid,imagepath from images where videoid = " & Me.intVideoID & "", Nothing, conImages)
            If dtImages IsNot Nothing Then
                If dtImages.Rows.Count > 0 Then
                    Dim drIsCompiled As DataTableReader = Database.FetchDataReader("select iscompiled from videos where videoid=" & intVideoID, Nothing, conImages)
                    If drIsCompiled.Read Then
                        If drIsCompiled.Item("iscompiled") = 0 Or drIsCompiled.Item("iscompiled") = -1 Then
                            If Directory.Exists(IMAGE_LOCAL_PATH & intVideoID) Then
                                Directory.Delete(IMAGE_LOCAL_PATH & intVideoID, True)
                            End If
                            Directory.CreateDirectory(IMAGE_LOCAL_PATH & intVideoID)

                            Dim ImageIndex As Integer = 1
                            For Each drRow As DataRow In dtImages.Rows
                                If SaveWebImages(drRow, ImageIndex) Then
                                    WriteToAppLog("Video ID - " & intVideoID & " Image Index : " & ImageIndex & " Downloaded Successfully")
                                    ImageIndex += 1
                                Else
                                    WriteToAppLog("Video ID - " & intVideoID & " image not downloaded")
                                End If
                            Next

                            If Not CompileVideo() Then
                                Throw New Exception("Video ID : " & intVideoID & " - Video Compiling Error. Process not completed.")
                            Else
                                Dim IsProcessingStatus As Integer = Database.ExecuteSQL("Select isprocessing from videos where videoid=" & intVideoID, Nothing, conImages)
                                If IsProcessingStatus = -1 Then
                                    Dim cmdUpdateProcessingStatus As New MySqlCommand("update videos set isprocessing=?isprocessing where videoid=?videoid", conImages)
                                    With cmdUpdateProcessingStatus
                                        With .Parameters
                                            Call .AddWithValue("isprocessing", 0)
                                            Call .AddWithValue("videoid", intVideoID)
                                        End With
                                        Call .ExecuteNonQuery()
                                    End With
                                End If
                            End If
                        End If
                        drIsCompiled.Close()
                    End If
                Else
                    UpdateVideoError("System.Exception: Image Not Found in Directory", intVideoID)
                End If
            End If
            RaiseEvent ProcessCompleted(Me)
        Catch ex As System.Exception
            ' UpdateVideoError(ex.ToString, Me.intVideoId)
            RaiseEvent ProcessNotCompleted(Me, ex)
            WriteToErrorLog("GetImages Procedure : " & ex.ToString)
        End Try
        If conImages IsNot Nothing Then conImages.Close() : conImages.Dispose()
    End Sub

    Private Function SaveWebImages(ByVal dtRow As DataRow, ByVal ImageIndex As Integer) As Boolean
        Dim intRetry As Integer = 0
        Dim Request As System.Net.HttpWebRequest = Nothing
        Dim Response As System.Net.HttpWebResponse = Nothing
        Dim ResponseStream As Stream = Nothing
        Dim FStream As FileStream = Nothing
        Do While intRetry < 3
            Try
                Dim ImageName As String = ""
                Dim ImagePath As String = dtRow.Item("imagepath")
                ImagePath = ImagePath.Trim
                Request = System.Net.WebRequest.Create(ImagePath)
                Response = CType(Request.GetResponse, System.Net.WebResponse)

                If Response.StatusCode = Net.HttpStatusCode.OK Then
                    If Request.HaveResponse Then

                        Dim FileName As String = IMAGE_LOCAL_PATH & intVideoID & "\" & ImageIndex.ToString & ".jpg"
                        ResponseStream = Response.GetResponseStream
                        FStream = New FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)
                        Dim data(4096) As Byte
                        Dim intOffset As Int64 = 0
                        Dim intReadBytes As Integer
                        Do
                            intReadBytes = ResponseStream.Read(data, 0, data.Length)
                            intOffset += intReadBytes
                            FStream.Write(data, 0, intReadBytes)
                        Loop While intReadBytes > 0
                        Call UpdateImageDatabase(dtRow.Item("imageid"))
                    Else
                        Throw New Exception("Request.HaveResponse return False")
                    End If
                Else
                    Throw New Exception("Status Code is Not OK - Status Code and Description : " & Response.StatusCode & " : " & Response.StatusDescription)
                End If
                SaveWebImages = True
                Exit Do
            Catch ex As Exception
                WriteToErrorLog("SaveWebImages Video ID -  " & intVideoID & " : " & ex.ToString)
                WriteToAppLog("SaveWebImages Video ID - " & intVideoID & " : " & ex.ToString)
                SaveWebImages = False
            End Try
            intRetry += 1
            If Not FStream Is Nothing Then FStream.Close() : FStream.Dispose()
            If Not Request Is Nothing Then Request.Abort()
        Loop
        If Not FStream Is Nothing Then FStream.Close()
        If Not FStream Is Nothing Then FStream.Dispose()
        If Not ResponseStream Is Nothing Then ResponseStream.Close()
        If Not ResponseStream Is Nothing Then ResponseStream.Dispose()
        If Not Request Is Nothing Then Request.Abort()
        If Not Response Is Nothing Then Response.Close()
    End Function

    Public Sub UpdateImageDatabase(ByVal imageid As Integer)
        Dim conUpdateImageData As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conUpdateImageData = Database.Connection.Clone
            If Not conUpdateImageData.State = ConnectionState.Open Then Call conUpdateImageData.Open()
            Dim strError As String = ""
            Dim cmdImages As New MySqlCommand("update images set isdownloaded=?isdownloaded, downloadedon=?downloadedon where imageid=?imageid", conUpdateImageData)
            With cmdImages
                With .Parameters
                    Call .AddWithValue("isdownloaded", 1)
                    Call .AddWithValue("downloadedon", Format(Now, "yyyy-MM-dd HH:mm:ss"))
                    Call .AddWithValue("imageid", imageid)
                End With
                Call .ExecuteNonQuery()
            End With
        Catch ex As Exception
            UpdateVideoError(ex.ToString, Me.intVideoID)
            WriteToErrorLog("UpdateImageDatabase Procedure : " & ex.ToString)
        End Try
        If conUpdateImageData IsNot Nothing Then conUpdateImageData.Close() : conUpdateImageData.Dispose()
    End Sub

    Private Function CompileVideo() As Boolean
        Dim conCompileVideo As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conCompileVideo = Database.Connection.Clone
            If Not conCompileVideo.State = ConnectionState.Open Then Call conCompileVideo.Open()

            Dim LastImaneName As String = ""
            Dim LastRect1 As RectangleF = Nothing
            Dim LastRect2 As RectangleF = Nothing

            Dim strStatement As String = "SELECT ifnull(users.country, '') as country FROM users " & _
                                        "INNER JOIN videos ON users.userid = videos.userid " & _
                                        "WHERE videos.videoid=" & intVideoID

            Dim strCountry As String = Database.ExecuteSQL(strStatement, Nothing, conCompileVideo)

            If strCountry = "za" Then
                VideoWidth = 640
                VideoHeight = 360
                blnMakeHDVideo = False
            Else
                VideoWidth = 1280
                VideoHeight = 720
                blnMakeHDVideo = True
            End If

            'Resize Large Image in Downloaded Image
            Call ResizeLargeImage()

            'Initialize animage video object
            video = New Animage.AnimageVideo(VideoWidth, VideoHeight, FPS, QUALITY)

            'Download Overlayimage (Logo)
            Dim drOverlay As DataTableReader = Database.FetchDataReader("Select overlayimagepath, isdownloadedoverlayimage from videos where videoid=" & intVideoID, Nothing, conCompileVideo)
            If drOverlay.Read Then
                If drOverlay.Item("isdownloadedoverlayimage") = 0 Then
                    If DownloadOverlayImage(drOverlay.Item("overlayimagepath")) Then
                        Dim cmdUpdateOverlay As New MySqlCommand("update videos set isdownloadedoverlayimage=?isdownloadedoverlayimage where videoid=?videoid", conCompileVideo)
                        With cmdUpdateOverlay
                            With .Parameters
                                Call .AddWithValue("isdownloadedoverlayimage", 1)
                                Call .AddWithValue("videoid", Me.intVideoID)
                            End With
                            Call .ExecuteNonQuery()
                        End With
                    Else
                        drOverlay.Close()
                        Throw New Exception("Video ID - " & intVideoID & " Video not compiled because Overlay Image not downloaded")
                    End If
                End If
            Else
                drOverlay.Close()
                Throw New Exception("Video ID - " & intVideoID & " : Undefine Overlay image information")
            End If
            drOverlay.Close()

            'Download AgentPhoto
            Dim drAgentInfo As DataTableReader = Database.FetchDataReader("Select propertyagentid, agentimage, isphotodownloaded from propertyagents where videoid=" & intVideoID, Nothing, conCompileVideo)
            If drAgentInfo.Read Then
                If drAgentInfo.Item("isphotodownloaded") = 0 Then
                    If DownloadAgentImage(drAgentInfo.Item("agentimage")) Then
                        Try
                            'Update isdownloaded set to 1 of propertyagents table
                            Dim cmdPropertyAgent As New MySqlCommand("update propertyagents set isphotodownloaded=?isphotodownloaded where propertyagentid=?propertyagentid", conCompileVideo)
                            With cmdPropertyAgent
                                With .Parameters
                                    Call .AddWithValue("isphotodownloaded", 1)
                                    Call .AddWithValue("propertyagentid", drAgentInfo.Item("propertyagentid"))
                                End With
                                Call .ExecuteNonQuery()
                            End With
                        Catch ex As Exception
                            WriteToAppLog("Video ID - " & intVideoID & " : Property agent's photo downloaded status not updated")
                            WriteToErrorLog("Video ID - " & intVideoID & " : Property agent's photo downloaded status not updated")
                        End Try
                    Else
                        drAgentInfo.Close()
                        Throw New Exception("Video ID - " & intVideoID & " Video not compiled because Agent Photo not downloaded")
                    End If
                End If
            Else
                drAgentInfo.Close()
                Throw New Exception("Video ID - " & intVideoID & " : Undefine Agent image information")
            End If
            drAgentInfo.Close()

            ''Download Map Images
            'Dim drLocation As DataTableReader = Database.FetchDataReader("Select propertydetailid, location, ismapdownloaded from propertydetails where videoid=" & intVideoID, Nothing, conCompileVideo)
            'If drLocation.Read Then
            '    If drLocation.Item("ismapdownloaded") = 0 Then
            '        If DownloadLocationMapImage(drLocation.Item("location")) Then
            '            Try
            '                'Update ismapdownloaded set to 1 of propertydetails table
            '                Dim cmdPropertyLocation As New MySqlCommand("update propertydetails set ismapdownloaded=?ismapdownloaded where propertydetailid=?propertydetailid", conCompileVideo)
            '                With cmdPropertyLocation
            '                    With .Parameters
            '                        Call .AddWithValue("ismapdownloaded", 1)
            '                        Call .AddWithValue("propertydetailid", drLocation.Item("propertydetailid"))
            '                    End With
            '                    Call .ExecuteNonQuery()
            '                End With
            '            Catch ex As Exception
            '                WriteToAppLog("Video ID - " & intVideoID & " : Property Map image downloaded status not updated")
            '                WriteToErrorLog("Video ID - " & intVideoID & " : Property Map image downloaded status not updated")
            '            End Try
            '        Else
            '            drLocation.Close()
            '            Throw New Exception("Video ID - " & intVideoID & " Video not compiled because Location Map image not downloaded")
            '        End If
            '    End If
            'Else
            '    drLocation.Close()
            '    Throw New Exception("Video ID - " & intVideoID & " : Undefine Location information")
            'End If
            'drLocation.Close()


            Dim NextPanningEffect As Integer = HorizontalPanningEffect.RightToLeft
            Dim NextVerticalPanningEffect As Integer = VerticalPanningEffect.TopToBottom

            If Directory.Exists(IMAGE_LOCAL_PATH & intVideoID) Then

                Dim strFiles As String() = Directory.GetFiles(IMAGE_LOCAL_PATH & intVideoID)
                'Dim FileNames() As Integer = Directory.GetFiles(strImageLocalPath & intVideoId)
                Dim FKey() As Integer = Nothing
                Dim index As Integer = 0
                For Each strFile As String In strFiles
                    Dim fname As String = Path.GetFileName(strFile)
                    ReDim Preserve FKey(index)
                    FKey(index) = CType(fname.Substring(0, fname.Length - 4), Integer)
                    index += 1
                Next
                If Not FKey Is Nothing Then
                    If FKey.Length > 0 Then
                        Array.Sort(FKey)
                    End If
                Else
                    Throw New Exception("Image Not Found in Directory")
                End If

                Dim DownloadImageCount As Integer = 0

                Dim ZoomIndex As Integer = 0
                Dim SlideIndex As Integer = 0
                For Each Fname As Integer In FKey

                    SLIDE_DURATION = 7
                    VERTICAL_SLIDE_DURATION = 14

                    Dim images As Image = Nothing
                    Try
                        images = Image.FromFile(IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg") 'Image.FromFile(strImageLocalPath & "\" & intVideoId & "\" & i & ".jpg")
                        DownloadImageCount += 1
                        SlideIndex += 1
                    Catch ex As Exception
                        WriteToAppLog("Image Corrupted - Image Path - " & IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg")
                        WriteToErrorLog("Image Corrupted - Image Path - " & IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg")
                    End Try

                    If Not images Is Nothing Then
                        ZoomIndex += 1

                        Dim x As Double = 0
                        Dim y As Double = 0

                        '' Variables for zoom effects
                        Dim ZoomX As Double = 0
                        Dim ZoomY As Double = 0
                        Dim ZoomWidth As Double = 0
                        Dim ZoomHeight As Double = 0

                        If images.Width >= images.Height Then

                            Dim h As Double = images.Height
                            Dim w As Double = (h * VideoWidth) / VideoHeight

                            If images.Width < w Then
                                Dim ResizeHeight As Double = images.Height - (images.Height * 20 / 100)
                                Call CheckWidth(images.Width, ResizeHeight)
                                h = HResizedHeight
                                w = HResizedWidth
                                'y = ResizeY
                                y = (images.Height - h) / 2
                            End If

                            Dim WidthDiff As Integer = images.Width - w
                            If WidthDiff <= 5 And h = images.Height Then
                                w = images.Width - images.Width * 20 / 100
                                h = images.Height - images.Height * 20 / 100
                                y = (images.Height - h) / 2
                            End If
                            'If w = images.Width And h = images.Height Then
                            '    w = images.Width - images.Width * 20 / 100
                            '    h = images.Height - images.Height * 20 / 100
                            'End If
                            x = images.Width - w


                            Dim Rect1 As RectangleF
                            Dim Rect2 As RectangleF

                            If ZoomIndex = 4 Then ' Zoom Out

                                ZoomHeight = h - ((h * ZoomOutPercent) / 100)
                                ZoomWidth = (ZoomHeight * VideoWidth) / VideoHeight
                                ZoomX = (images.Width - ZoomWidth) / 2
                                ZoomY = (images.Height - ZoomHeight) / 2

                                x = x / 2
                                y = y / 2

                                Rect1 = New RectangleF(ZoomX, ZoomY, ZoomWidth, ZoomHeight)
                                Rect2 = New RectangleF(x, y, w, h)

                            ElseIf ZoomIndex = 8 Then 'Zoom Out With Diagonal Panning

                                y = images.Height - h

                                ZoomHeight = h - ((h * ZoomOutPercent) / 100)
                                ZoomWidth = (ZoomHeight * VideoWidth) / VideoHeight
                                'ZoomX = (images.Width - ZoomWidth) / 2
                                'ZoomY = (images.Height - ZoomHeight) / 2

                                ZoomX = (images.Width - ZoomWidth)
                                ZoomY = (images.Height - ZoomHeight)

                                'x = x / 2
                                'y = y / 2

                                If NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom Then
                                    Rect1 = New RectangleF(0, 0, ZoomWidth, ZoomHeight)
                                    Rect2 = New RectangleF(x, y, w, h)
                                    NextVerticalPanningEffect = VerticalPanningEffect.BottomToTop
                                Else
                                    Rect1 = New RectangleF(0, y, ZoomWidth, ZoomHeight)
                                    Rect2 = New RectangleF(x, 0, w, h)
                                    NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom
                                End If

                                SLIDE_DURATION += 4

                            ElseIf ZoomIndex = 12 Then 'Top to bottom No Diagonal Effect
                                h = h - ((h * 15) / 100)
                                w = (h * VideoWidth) / VideoHeight

                                x = images.Width - w
                                y = images.Height - h

                                Rect1 = New RectangleF(0, 0, w, h)
                                Rect2 = New RectangleF(x, y, w, h)

                                SLIDE_DURATION += 7

                                ZoomIndex = 0
                            Else
                                If NextPanningEffect = HorizontalPanningEffect.RightToLeft Then
                                    Rect1 = New RectangleF(0, y, w, h)
                                    Rect2 = New RectangleF(x, y, w, h)
                                    NextPanningEffect = HorizontalPanningEffect.LeftToRight
                                Else
                                    Rect1 = New RectangleF(x, y, w, h)
                                    Rect2 = New RectangleF(0, y, w, h)
                                    NextPanningEffect = HorizontalPanningEffect.RightToLeft
                                End If
                            End If
                            images.Dispose()

                            'Dim imagePath As String = strImageLocalPath & intVideoId & "\" & i & ".jpg"
                            If SlideIndex = 1 Then
                                Call video.Slides.Add(IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg", Rect1, Rect2, INTRO_SLIDE_DURATION, New Animage.AnimageFadeTransition(1))

                                If File.Exists(Application.StartupPath & "/Maps/" & intVideoID & ".jpg") Then
                                    'ZoomOut Effect for Map Screen
                                    'Call video.Slides.Add(Application.StartupPath & "/Maps/" & intVideoID & ".jpg", New RectangleF(256, 144, 768, 432), New RectangleF(0, 0, 1280, 720), SLIDE_DURATION, New Animage.AnimageFadeTransition(1))
                                    Call video.Slides.Add(Application.StartupPath & "/Maps/" & intVideoID & ".jpg", New RectangleF(160, 90, 960, 540), New RectangleF(0, 0, 1280, 720), SLIDE_DURATION, New Animage.AnimageFadeTransition(1))
                                    Call video.Slides.Add(Application.StartupPath & "/Maps/" & intVideoID & ".jpg", New RectangleF(0, 0, 1280, 720), New RectangleF(0, 0, 1280, 720), 2.5, New Animage.AnimageNoneTransition)
                                End If
                            End If
                            Call video.Slides.Add(IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg", Rect1, Rect2, SLIDE_DURATION, New Animage.AnimageFadeTransition(1))
                            LastImaneName = Fname.ToString
                            LastRect1 = Rect1
                            LastRect2 = Rect2

                        ElseIf images.Height > images.Width Then

                            Dim w As Double = images.Width
                            Dim h As Double = (w * VideoHeight) / VideoWidth

                            If images.Height < h Then
                                Dim ResizeWidth As Double = images.Width - (images.Width * 20 / 100)
                                Call CheckHeight(images.Height, ResizeWidth)
                                h = VResizedHeight
                                w = VResizedWidth
                                'x = ResizeX
                                x = (images.Width - w) / 2
                            End If

                            Dim HeightDiff As Integer = images.Height - h
                            If w = images.Width And HeightDiff <= 5 Then
                                w = images.Width - images.Width * 20 / 100
                                h = images.Height - images.Height * 20 / 100
                                x = (images.Width - w) / 2
                            End If


                            'If w = images.Width And h = images.Height Then
                            '    w = images.Width - images.Height * 20 / 100
                            '    h = images.Height - images.Height * 20 / 100
                            'End If

                            y = images.Height - h

                            Dim Rect1 As RectangleF
                            Dim Rect2 As RectangleF

                            If ZoomIndex = 4 Then 'Zoom Out 

                                ZoomHeight = h - ((h * ZoomOutPercent) / 100)
                                ZoomWidth = (ZoomHeight * VideoWidth) / VideoHeight
                                ZoomX = (images.Width - ZoomWidth) / 2
                                ZoomY = (images.Height - ZoomHeight) / 2

                                x = x / 2
                                y = y / 2

                                Rect1 = New RectangleF(ZoomX, ZoomY, ZoomWidth, ZoomHeight)
                                Rect2 = New RectangleF(x, y, w, h)

                            ElseIf ZoomIndex = 8 Then ' Zoom Out With Diagonal Panning

                                ZoomWidth = w - ((w * ZoomOutPercent) / 100)
                                ZoomHeight = (ZoomWidth * VideoHeight) / VideoWidth
                                ZoomX = (images.Width - ZoomWidth) / 2
                                ZoomY = (images.Height - ZoomHeight) / 2

                                'x = x / 2
                                'y = y / 2
                                If NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom Then
                                    Rect1 = New RectangleF(0, 0, ZoomWidth, ZoomHeight)
                                    Rect2 = New RectangleF(x, y, w, h)
                                    NextVerticalPanningEffect = VerticalPanningEffect.BottomToTop
                                Else
                                    Rect1 = New RectangleF(0, y, ZoomWidth, ZoomHeight)
                                    Rect2 = New RectangleF(x, 0, w, h)
                                    NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom
                                End If

                            ElseIf ZoomIndex = 12 Then ' Top to bottom panning
                                If NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom Then
                                    'Simple Panning
                                    Rect1 = New RectangleF(x, 0, w, h)
                                    Rect2 = New RectangleF(x, y, w, h)
                                    NextVerticalPanningEffect = VerticalPanningEffect.BottomToTop
                                Else
                                    'Simple Panning
                                    Rect1 = New RectangleF(x, y, w, h)
                                    Rect2 = New RectangleF(x, 0, w, h)
                                    NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom
                                End If
                                ZoomIndex = 0
                            Else
                                If NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom Then
                                    'Simple Panning
                                    Rect1 = New RectangleF(x, 0, w, h)
                                    Rect2 = New RectangleF(x, y, w, h)

                                    NextVerticalPanningEffect = VerticalPanningEffect.BottomToTop
                                Else
                                    'Simple Panning
                                    Rect1 = New RectangleF(x, y, w, h)
                                    Rect2 = New RectangleF(x, 0, w, h)

                                    NextVerticalPanningEffect = VerticalPanningEffect.TopToBottom
                                End If
                            End If
                            images.Dispose()

                            If SlideIndex = 1 Then
                                Call video.Slides.Add(IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg", Rect1, Rect2, INTRO_SLIDE_DURATION, New Animage.AnimageFadeTransition(1))

                                If File.Exists(Application.StartupPath & "/Maps/" & intVideoID & ".jpg") Then
                                    'ZoomOut Effect for Map Screen
                                    'Call video.Slides.Add(Application.StartupPath & "/Maps/" & intVideoID & ".jpg", New RectangleF(256, 144, 768, 432), New RectangleF(0, 0, 1280, 720), SLIDE_DURATION, New Animage.AnimageFadeTransition(1))
                                    Call video.Slides.Add(Application.StartupPath & "/Maps/" & intVideoID & ".jpg", New RectangleF(160, 90, 960, 540), New RectangleF(0, 0, 1280, 720), SLIDE_DURATION, New Animage.AnimageFadeTransition(1))
                                    Call video.Slides.Add(Application.StartupPath & "/Maps/" & intVideoID & ".jpg", New RectangleF(0, 0, 1280, 720), New RectangleF(0, 0, 1280, 720), 2.5, New Animage.AnimageNoneTransition)
                                End If
                            End If
                            Call video.Slides.Add(IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg", Rect1, Rect2, VERTICAL_SLIDE_DURATION, New Animage.AnimageFadeTransition(1))
                            LastImaneName = Fname.ToString
                            LastRect1 = Rect1
                            LastRect2 = Rect2
                        End If
                    End If
                Next

                'Generate Narration
                intVoiceOver = Database.ExecuteSQL("SELECT voiceover FROM videos WHERE videoid=" & intVideoID, Nothing, conCompileVideo)

                If intVoiceOver = Voices.Kate Then
                    intNarrationDuration = GenerateNarration(Voices.Kate)
                ElseIf intVoiceOver = Voices.Bridget Then
                    intNarrationDuration = GenerateNarration(Voices.Bridget)
                End If

                Dim Close_Slide_Duration As Integer

                Dim intVideoSeconds As Integer = (video.Slides.TotalFrames / video.FPS)
                Dim TotalVideoSecond As Integer = (intVideoSeconds + CLOSED_SLIDE_DURATION + 2) - 1 'Last two slide duration excluding Transition duration that is 1

                If intNarrationDuration > TotalVideoSecond Then
                    Close_Slide_Duration = intNarrationDuration - intVideoSeconds
                Else
                    Close_Slide_Duration = CLOSED_SLIDE_DURATION
                End If

                Call video.Slides.Add(IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & LastImaneName.ToString & ".jpg", LastRect1, LastRect2, Close_Slide_Duration, New Animage.AnimageFadeTransition(1))
                video.Slides.Add(Application.StartupPath & "/black.jpg", New RectangleF(0, 0, 600, 450), New Rectangle(0, 0, 600, 450), 2, New Animage.AnimageFadeTransition(1))

                Dim TotalImages As Integer = video.Slides.Count
                If TotalImages > 1 Then

                    Dim TotalImageCount As Integer = Database.ExecuteSQL("Select Count(*) as imagecount from images where videoid=" & intVideoID, Nothing, conCompileVideo)
                    Dim ImageDownloedPercent As Integer = DownloadImageCount * 100 / TotalImageCount
                    If ImageDownloedPercent > MIN_IMAGE_DOWNLOADED_PERCENTAGE Then
                        If AddOverlays() Then
                            video.MusicPath = GetFinalMusic()
                            Dim VideoPath As String = video.Compile()
                            If File.Exists(VideoPath) Then
                                If ModifyVideoHeader(VideoPath) Then
                                    Dim CompileTime As TimeSpan = video.CompileTime
                                    Dim Mp4VideoLength As Long = Common.GetFileSize(VideoPath)
                                    Call UpdateVideoDatabase(VideoPath, CompileTime.TotalSeconds, TotalImages, Mp4VideoLength)
                                    WriteToAppLog("Video ID : " & Me.intVideoID & " Video Compile Successfully...")
                                Else
                                    WriteToAppLog("VideoID - " & intVideoID & " : Video Header modified error")
                                    WriteToErrorLog("VideoID - " & intVideoID & " : Video Header modified error")
                                    Throw New Exception("VideoID - " & intVideoID & " : Video Header modified error")
                                End If

                                CompileVideo = True
                            Else
                                WriteToAppLog("VideoID - " & intVideoID & " : Video compiling error video path not found")
                                WriteToErrorLog("VideoID - " & intVideoID & " : Video compiling error video path not found")
                                CompileVideo = False
                            End If
                        Else
                            WriteToAppLog("VideoID - " & intVideoID & " : Video were not compiled because AddOverlays function failed")
                            WriteToErrorLog("VideoID - " & intVideoID & " : Video were not compiled because AddOverlays function failed")
                            Throw New Exception("VideoID - " & intVideoID & " : Video were not compiled because AddOverlays function failed")
                        End If
                    Else
                        WriteToAppLog("Video Compiling Failed because Images were not downloaded more then 80%, Total Images-" & TotalImageCount & "/Downloaded Images-" & DownloadImageCount)
                        Throw New Exception("Video Compiling Failed because Images were not downloaded more then 80%")
                    End If
                Else
                    Throw New Exception("Image Not Found in Directory")
                End If

            Else
                WriteToErrorLog("VideoID - " & intVideoID & " : Image directory not exist")
                CompileVideo = False
            End If

        Catch ex As Exception
            UpdateVideoError(ex.ToString, Me.intVideoID)
            WriteToErrorLog("CompileVideo Function : " & ex.ToString)
            CompileVideo = False
        End Try
        If conCompileVideo IsNot Nothing Then conCompileVideo.Close() : conCompileVideo.Dispose()
    End Function

    Private Sub ResizeLargeImage()
        Try
            Dim strFiles As String() = Directory.GetFiles(IMAGE_LOCAL_PATH & intVideoID)

            Dim FKey() As Integer = Nothing
            Dim index As Integer = 0
            For Each strFile As String In strFiles
                Dim fname As String = Path.GetFileName(strFile)
                ReDim Preserve FKey(index)
                FKey(index) = CType(fname.Substring(0, fname.Length - 4), Integer)
                index += 1
            Next

            If Not FKey Is Nothing Then
                If FKey.Length > 0 Then
                    Array.Sort(FKey)
                End If
            Else
                Throw New Exception("Image Not Found in Directory")
            End If

            For Each Fname As Integer In FKey
                Try
                    Dim Img As Image = Nothing
                    Try
                        Img = Image.FromFile(IMAGE_LOCAL_PATH & intVideoID & "\" & Fname.ToString & ".jpg")
                    Catch ex As Exception
                        WriteToErrorLog("ResizeLargeImage Procedure : " & Fname & ".jpg image is Corrupted - Exception : " & ex.ToString)
                    End Try
                    If Img IsNot Nothing Then
                        Dim blnResize As Boolean = False
                        Dim ResizeWidth As Integer = 0
                        Dim ResizeHeight As Integer = 0
                        If Img.Width > Img.Height Then ' LandScap Image

                            If Img.Width > VideoWidth Then
                                'Resize Image in HD Width
                                ResizeWidth = VideoWidth
                                ResizeHeight = (VideoWidth * Img.Height) / Img.Width

                                blnResize = True
                            Else
                                blnResize = False
                            End If
                        Else ' Potrait Image
                            If Img.Height > VideoHeight Then
                                'Resize Image HD Height
                                ResizeHeight = VideoHeight
                                ResizeWidth = (VideoHeight * Img.Width) / Img.Height
                                blnResize = True
                            Else
                                blnResize = False
                            End If
                        End If

                        If blnResize Then

                            If Not Directory.Exists(IMAGE_LOCAL_PATH & intVideoID & "\Resize") Then
                                Directory.CreateDirectory(IMAGE_LOCAL_PATH & intVideoID & "\Resize")
                            End If

                            Using bmp = New Bitmap(ResizeWidth, ResizeHeight)
                                'Using img = Image.FromFile(SourceImage)
                                Using g = Graphics.FromImage(bmp)

                                    g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                                    g.DrawImage(Img, New RectangleF(0, 0, ResizeWidth, ResizeHeight))

                                    Dim jgpEncoder As ImageCodecInfo = GetEncoder(ImageFormat.Jpeg)
                                    Dim myEncoder As System.Drawing.Imaging.Encoder = System.Drawing.Imaging.Encoder.Quality

                                    Dim myEncoderParameter As New EncoderParameter(myEncoder, 100)
                                    Dim myEncoderParameters As New EncoderParameters(1)
                                    myEncoderParameters.Param(0) = myEncoderParameter
                                    bmp.Save(IMAGE_LOCAL_PATH & intVideoID & "\Resize\" & Fname.ToString & ".jpg", jgpEncoder, myEncoderParameters)
                                End Using
                            End Using

                            If Img IsNot Nothing Then Img.Dispose()

                            'Copy file to main Folder
                            File.Copy(IMAGE_LOCAL_PATH & intVideoID & "\Resize\" & Fname.ToString & ".jpg", IMAGE_LOCAL_PATH & intVideoID & "\" & Fname.ToString & ".jpg", True)
                        End If
                    End If
                Catch ex As Exception
                    WriteToErrorLog("ResizeLargeImage Procedure : Failed to Resize Image " & Fname.ToString & ".jpg - Exception : " & ex.ToString)
                End Try
            Next
        Catch ex As Exception
            WriteToErrorLog("ResizeLargeImage Procedure Exception : " & ex.ToString)
        End Try
    End Sub

    Private Function GetEncoder(ByVal format As ImageFormat) As ImageCodecInfo

        Dim codecs As ImageCodecInfo() = ImageCodecInfo.GetImageDecoders()

        Dim codec As ImageCodecInfo
        For Each codec In codecs
            If codec.FormatID = format.Guid Then
                Return codec
            End If
        Next codec

        Return Nothing

    End Function

    Private Function ModifyVideoHeader(ByVal VideoPath As String) As Boolean
        Dim blnSuccess As Boolean = False
        Try
            'Rename Main final.mp4 to final1.mp4
            Dim strDestinationPath As String = VideoPath.Substring(0, VideoPath.Length - 9)
            strDestinationPath = strDestinationPath & "final1.mp4"

            Try
                File.Move(VideoPath, strDestinationPath)
            Catch ex As Exception
                Throw New Exception("File Move Error : " & ex.ToString)
            End Try

            'Check final1.mp4 Exist for convert video
            If File.Exists(strDestinationPath) Then
                Dim QtProcess As New Process

                Dim result As String = ""
                Dim errorreader As StreamReader = Nothing

                QtProcess.StartInfo.UseShellExecute = False
                QtProcess.StartInfo.ErrorDialog = False
                QtProcess.StartInfo.RedirectStandardError = True
                QtProcess.StartInfo.CreateNoWindow = True
                QtProcess.StartInfo.FileName = """" & Application.StartupPath & "\libs\qt-faststart\qt-faststart" & """"
                QtProcess.StartInfo.Arguments = """" & strDestinationPath & """" & " " & """" & VideoPath & """"
                QtProcess.Start()
                errorreader = QtProcess.StandardError
                result = errorreader.ReadToEnd()
                QtProcess.WaitForExit()
                WriteToAppLog("Video " & intVideoID & " Header Modified Successfully")

                QtProcess.Close()
                QtProcess.Dispose()

                If Not File.Exists(VideoPath) Then
                    Throw New Exception("Mp4 Converted Failed")
                End If
                blnSuccess = True
            Else
                Throw New Exception("ModifyVideoHeader Procedure : File Not Found")
            End If
        Catch ex As Exception
            WriteToErrorLog("ModifyVideoHeader Procedure - Video ID - " & intVideoID & " : " & ex.ToString)
            WriteToAppLog("ModifyVideoHeader Procedure - Video ID - " & intVideoID & " : " & ex.ToString)
            blnSuccess = False
        End Try
        Return blnSuccess
    End Function

    Private Function DownloadAgentImage(ByVal AgentImagePath As String) As Boolean
        Dim Request As System.Net.HttpWebRequest = Nothing
        Dim Response As System.Net.HttpWebResponse = Nothing
        Dim ResponseStream As Stream = Nothing
        Dim FStream As FileStream = Nothing
        Try
            If Not Directory.Exists(Application.StartupPath & "/AgentPhotos") Then
                Directory.CreateDirectory(Application.StartupPath & "/AgentPhotos")
            End If

            AgentImagePath = AgentImagePath.Trim
            Request = System.Net.WebRequest.Create(AgentImagePath)
            Response = CType(Request.GetResponse, System.Net.WebResponse)

            If Request.HaveResponse Then

                Dim FileName As String = Application.StartupPath & "/AgentPhotos/" & intVideoID.ToString & ".jpg"
                ResponseStream = Response.GetResponseStream
                FStream = New FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)
                Dim data(4096) As Byte
                Dim intOffset As Int64 = 0
                Dim intReadBytes As Integer
                Do
                    intReadBytes = ResponseStream.Read(data, 0, data.Length)
                    intOffset += intReadBytes
                    FStream.Write(data, 0, intReadBytes)
                Loop While intReadBytes > 0
            End If
            WriteToAppLog("Video ID - " & intVideoID & " : AgentPhoto downloaded successfully")
            DownloadAgentImage = True
        Catch ex As Exception
            WriteToErrorLog("DownloadAgentImage Video ID - " & intVideoID & " : " & ex.ToString)
            WriteToAppLog("DownloadAgentImage Video ID - " & intVideoID & " : " & ex.ToString)
            DownloadAgentImage = False
        End Try

        If Not FStream Is Nothing Then FStream.Close()
        If Not FStream Is Nothing Then FStream.Dispose()
        If Not ResponseStream Is Nothing Then ResponseStream.Close()
        If Not ResponseStream Is Nothing Then ResponseStream.Dispose()
        If Not Request Is Nothing Then Request.Abort()
        If Not Response Is Nothing Then Response.Close()
    End Function

    Private Function DownloadLocationMapImage(ByVal Location As String) As Boolean
        Dim intRetry As Integer = 0
        Dim blnDonwloaded As Boolean = False
        Do While intRetry < 3 'No of time retry while downloading image

            Dim Request As System.Net.HttpWebRequest = Nothing
            Dim Response As System.Net.HttpWebResponse = Nothing
            Dim ResponseStream As Stream = Nothing
            Dim FStream As FileStream = Nothing
            Try
                If Not Directory.Exists(Application.StartupPath & "/Maps") Then
                    Directory.CreateDirectory(Application.StartupPath & "/Maps")
                End If

                If File.Exists(Application.StartupPath & "/Maps/" & intVideoID.ToString & ".jpg") Then
                    Try
                        File.Delete(Application.StartupPath & "/Maps/" & intVideoID.ToString & ".jpg")
                    Catch ex As Exception
                        WriteToAppLog("DownloadLocationMapImage Procedure : File Deletion Error - " & ex.ToString)
                        WriteToErrorLog("DownloadLocationMapImage Procedure : File Deletion Error - " & ex.ToString)
                    End Try
                End If

                Dim MapURL As String = "http://maps.googleapis.com/maps/api/staticmap?center=" & Location & "&zoom=10&size=640x360&scale=2&sensor=false&markers=color:blue|" & Location.ToString & "&key=AIzaSyAQOshVCj20T7-C8wvBQ_hmugXcX6twZe4"
                MapURL = MapURL.Trim
                Request = System.Net.WebRequest.Create(MapURL)
                Response = CType(Request.GetResponse, System.Net.WebResponse)

                If Request.HaveResponse Then
                    Dim FileName As String = Application.StartupPath & "/Maps/" & intVideoID.ToString & ".jpg"
                    ResponseStream = Response.GetResponseStream
                    FStream = New FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)
                    Dim data(4096) As Byte
                    Dim intOffset As Int64 = 0
                    Dim intReadBytes As Integer
                    Do
                        intReadBytes = ResponseStream.Read(data, 0, data.Length)
                        intOffset += intReadBytes
                        FStream.Write(data, 0, intReadBytes)
                    Loop While intReadBytes > 0
                End If
                WriteToAppLog("Video ID - " & intVideoID & " : Location Map image downloaded successfully")
                blnDonwloaded = True
            Catch ex As Exception
                blnDonwloaded = False
                WriteToErrorLog("DownloadLocationMapImage Video ID - " & intVideoID & " : " & ex.ToString)
                WriteToAppLog("DownloadLocationMapImage Video ID - " & intVideoID & " : " & ex.ToString)
            End Try
            If Not FStream Is Nothing Then FStream.Close()
            If Not FStream Is Nothing Then FStream.Dispose()
            If Not ResponseStream Is Nothing Then ResponseStream.Close()
            If Not ResponseStream Is Nothing Then ResponseStream.Dispose()
            If Not Request Is Nothing Then Request.Abort()
            If Not Response Is Nothing Then Response.Close()
            If blnDonwloaded Then
                Exit Do
            End If
            intRetry += 1
        Loop
        Return blnDonwloaded
    End Function


    Private Function DownloadOverlayImage(ByVal OverlayImagePath As String) As Boolean
        Dim Request As System.Net.HttpWebRequest = Nothing
        Dim Response As System.Net.HttpWebResponse = Nothing
        Dim ResponseStream As Stream = Nothing
        Dim FStream As FileStream = Nothing
        Try
            If Not Directory.Exists(Application.StartupPath & "/OverlayImages") Then
                Directory.CreateDirectory(Application.StartupPath & "/OverlayImages")
            End If

            OverlayImagePath = OverlayImagePath.Trim
            Request = System.Net.WebRequest.Create(OverlayImagePath)
            Response = CType(Request.GetResponse, System.Net.WebResponse)

            If Request.HaveResponse Then

                Dim FileName As String = Application.StartupPath & "/OverlayImages/" & intVideoID.ToString & ".jpg"
                ResponseStream = Response.GetResponseStream
                FStream = New FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)
                Dim data(4096) As Byte
                Dim intOffset As Int64 = 0
                Dim intReadBytes As Integer
                Do
                    intReadBytes = ResponseStream.Read(data, 0, data.Length)
                    intOffset += intReadBytes
                    FStream.Write(data, 0, intReadBytes)
                Loop While intReadBytes > 0
            End If
            WriteToAppLog("Video ID - " & intVideoID & " : OverlayImage downloaded successfully")
            DownloadOverlayImage = True
        Catch ex As Exception
            WriteToErrorLog("DonwloadOverlayImage Video ID - " & intVideoID & " : " & ex.ToString)
            WriteToAppLog("DonwloadOverlayImage Video ID - " & intVideoID & " : " & ex.ToString)
            DownloadOverlayImage = False
        End Try

        If Not FStream Is Nothing Then FStream.Close()
        If Not FStream Is Nothing Then FStream.Dispose()
        If Not ResponseStream Is Nothing Then ResponseStream.Close()
        If Not ResponseStream Is Nothing Then ResponseStream.Dispose()
        If Not Request Is Nothing Then Request.Abort()
        If Not Response Is Nothing Then Response.Close()
    End Function

    Private Function AddOverlays() As Boolean
        Dim conOverlays As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conOverlays = Database.Connection.Clone
            If Not conOverlays.State = ConnectionState.Open Then Call conOverlays.Open()
            Dim dtOverlay As DataTable = Database.FetchDataTable("Select propertydetails.*, propertyagents.*, videos.theme, videos.iscommercial from propertydetails, propertyagents, videos  where propertydetails.videoid=videos.videoid and propertyagents.videoid=videos.videoid and videos.videoid=" & intVideoID, Nothing, conOverlays)
            Dim drOverlay As DataRow = Nothing
            If Not dtOverlay Is Nothing Then
                drOverlay = dtOverlay.Rows(0)

                If IsDBNull(drOverlay.Item("price")) Then
                    Throw New Exception("Price not found")
                ElseIf IsDBNull(drOverlay.Item("bedrooms")) Then
                    Throw New Exception("No of bedrooms not found")
                ElseIf IsDBNull(drOverlay.Item("bathrooms")) Then
                    Throw New Exception("No of bathrooms not found")
                End If
            End If

            Dim strPrefix As String = ""
            If blnMakeHDVideo Then
                strPrefix = "HD"
            Else
                strPrefix = "NHD"
            End If
            If Not Animage.FontPool.Count > 0 Then

                'For HD Videos
                Animage.FontPool.Add("HDPriceFont", New Font("Arial", 50, FontStyle.Regular))
                Animage.FontPool.Add("HDPtPriceFont", New Font("Arial", 50, FontStyle.Bold))
                Animage.FontPool.Add("HDNumFont", New Font("Arial", 34, FontStyle.Regular))
                Animage.FontPool.Add("HDPtNumFont", New Font("Arial", 36, FontStyle.Bold))
                Animage.FontPool.Add("HDTitle", New Font("Arial", 34, FontStyle.Regular))
                Animage.FontPool.Add("HDPtTitle", New Font("Arial", 36, FontStyle.Bold))
                Animage.FontPool.Add("HDCommercialTitle", New Font("Arial", 38, FontStyle.Bold))
                Animage.FontPool.Add("HDContactTitle", New Font("Arial", 44, FontStyle.Regular))
                Animage.FontPool.Add("HDAgentName", New Font("Arial", 32, FontStyle.Regular))
                Animage.FontPool.Add("HDLocation", New Font("Arial", 30, FontStyle.Regular))
                Animage.FontPool.Add("HDContactInfo", New Font("Arial", 26, FontStyle.Regular))

                'For Non HD videos 360p
                'Animage.FontPool.Add("NHDPriceFont", New Font("Arial", 28, FontStyle.Regular))
                'Animage.FontPool.Add("NHDPtPriceFont", New Font("Arial", 28, FontStyle.Bold))
                'Animage.FontPool.Add("NHDNumFont", New Font("Arial", 18, FontStyle.Regular))
                'Animage.FontPool.Add("NHDPtNumFont", New Font("Arial", 20, FontStyle.Bold))
                'Animage.FontPool.Add("NHDTitle", New Font("Arial", 18, FontStyle.Regular))
                'Animage.FontPool.Add("NHDPtTitle", New Font("Arial", 20, FontStyle.Bold))
                'Animage.FontPool.Add("NHDCommercialTitle", New Font("Arial", 20, FontStyle.Bold))
                'Animage.FontPool.Add("NHDContactTitle", New Font("Arial", 24, FontStyle.Regular))
                'Animage.FontPool.Add("NHDAgentName", New Font("Arial", 20, FontStyle.Regular))
                'Animage.FontPool.Add("NHDLocation", New Font("Arial", 20, FontStyle.Regular))
                'Animage.FontPool.Add("NHDContactInfo", New Font("Arial", 16, FontStyle.Regular))

                Animage.FontPool.Add("NHDPriceFont", New Font("Arial", 25, FontStyle.Regular))
                Animage.FontPool.Add("NHDPtPriceFont", New Font("Arial", 25, FontStyle.Bold))
                Animage.FontPool.Add("NHDNumFont", New Font("Arial", 17, FontStyle.Regular))
                Animage.FontPool.Add("NHDPtNumFont", New Font("Arial", 18, FontStyle.Bold))
                Animage.FontPool.Add("NHDTitle", New Font("Arial", 17, FontStyle.Regular))
                Animage.FontPool.Add("NHDPtTitle", New Font("Arial", 18, FontStyle.Bold))
                Animage.FontPool.Add("NHDCommercialTitle", New Font("Arial", 19, FontStyle.Bold))
                Animage.FontPool.Add("NHDContactTitle", New Font("Arial", 22, FontStyle.Regular))
                Animage.FontPool.Add("NHDAgentName", New Font("Arial", 16, FontStyle.Regular))
                Animage.FontPool.Add("NHDLocation", New Font("Arial", 15, FontStyle.Regular))
                Animage.FontPool.Add("NHDContactInfo", New Font("Arial", 13, FontStyle.Regular))

            End If

            Dim RectF As RectangleF
            If File.Exists(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg") Then
                RectF = GetLogoDimension()
            End If


            For slide As Integer = 0 To video.Slides.Count - 1
                With video.Slides(slide).SlideOverlays

                    Dim PriceRect As RectangleF
                    Dim LineRect As RectangleF
                    Dim CommercialLogoRect As RectangleF
                    Dim CommercialTitleRect As RectangleF
                    Dim ThemeRect As RectangleF
                    Dim PTNoBedroomRect As RectangleF
                    Dim PTBedTitle As RectangleF
                    Dim BedroomRect As RectangleF
                    Dim NoBedRoomRect As RectangleF
                    Dim BedroomTitleRect As RectangleF
                    Dim ThemeRect2 As RectangleF
                    Dim PtNoBathroomRect As RectangleF
                    Dim PTBathTitle As RectangleF
                    Dim BathroomRect As RectangleF
                    Dim NoBathroomRect As RectangleF
                    Dim BathroomTitleRect As RectangleF

                    Dim strCommercialLogoName As String = ""
                    Dim strThemeName As String = ""
                    Dim strBedroomName As String = ""
                    Dim strBathroomsName As String = ""

                    If blnMakeHDVideo Then
                        PriceRect = New RectangleF(116, 63, 636, 72)
                        LineRect = New RectangleF(116, 174, 636, 1)

                        CommercialLogoRect = New RectangleF(85, 280, 138, 138)
                        CommercialTitleRect = New RectangleF(283, 280, 800, 138)

                        ThemeRect = New RectangleF(85, 242, 120, 120)
                        ThemeRect2 = New RectangleF(85, 392, 120, 120)
                        PTBedTitle = New RectangleF(225, 242, 378, 120)
                        PTNoBedroomRect = New RectangleF(85, 242, 120, 120)
                        PtNoBathroomRect = New RectangleF(85, 392, 120, 120)
                        PTBathTitle = New RectangleF(225, 392, 378, 120)

                        BedroomRect = New RectangleF(116, 228, 258, 138)
                        NoBedRoomRect = New RectangleF(238, 228, 138, 138)
                        BedroomTitleRect = New RectangleF(420, 228, 378, 138)
                        BathroomRect = New RectangleF(116, 432, 258, 138)
                        NoBathroomRect = New RectangleF(238, 432, 138, 138)
                        BathroomTitleRect = New RectangleF(420, 432, 378, 138)

                        strCommercialLogoName = "commericial"
                        strThemeName = "theme1"
                        strBedroomName = "bedrooms"
                        strBathroomsName = "bathrooms"
                    Else
                        PriceRect = New RectangleF(58, 36, 350, 50)
                        LineRect = New RectangleF(58, 85, 350, 1)

                        CommercialLogoRect = New RectangleF(58, 140, 69, 70)
                        CommercialTitleRect = New RectangleF(157, 160, 800, 36)

                        'ThemeRect = New RectangleF(42, 120, 50, 50)
                        ThemeRect = New RectangleF(42, 120, 60, 60)
                        'ThemeRect2 = New RectangleF(42, 186, 50, 50)
                        ThemeRect2 = New RectangleF(42, 198, 60, 60)

                        'PTBedTitle = New RectangleF(119, 133, 170, 32)
                        PTBedTitle = New RectangleF(120, 138, 170, 32)

                        'PTNoBedroomRect = New RectangleF(42, 120, 50, 50)
                        PTNoBedroomRect = New RectangleF(42, 120, 60, 60)

                        'PTBathTitle = New RectangleF(119, 193, 170, 32)
                        PTBathTitle = New RectangleF(120, 213, 170, 32)

                        PtNoBathroomRect = New RectangleF(42, 198, 60, 60)

                        BedroomRect = New RectangleF(58, 112, 129, 69)
                        NoBedRoomRect = New RectangleF(119, 112, 68, 68)
                        BedroomTitleRect = New RectangleF(215, 120, 170, 50)
                        BathroomRect = New RectangleF(58, 214, 129, 69)
                        NoBathroomRect = New RectangleF(119, 214, 68, 68)
                        BathroomTitleRect = New RectangleF(215, 223, 170, 50)

                        strCommercialLogoName = "commercial360p"
                        strThemeName = "theme1360p"
                        strBedroomName = "bedrooms360p"
                        strBathroomsName = "bathrooms360p"
                    End If

                    If slide = 0 Then ' First Slide
                        'Add Black Overlay
                        .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, New RectangleF(0, 0, VideoWidth, VideoHeight), Pens.Transparent, New SolidBrush(Color.FromArgb(200, 0, 0, 0))))

                        'Add Price
                        Dim strPriceAlign As StringFormat = New StringFormat()
                        strPriceAlign.LineAlignment = StringAlignment.Near
                        strPriceAlign.Alignment = StringAlignment.Near
                        If drOverlay.Item("theme") = 1 Then
                            .Add(New Animage.AnimageTextOverlay(drOverlay.Item("price").ToString.Trim, strPrefix & "PtPriceFont", Brushes.White, PriceRect, strPriceAlign))
                        Else
                            .Add(New Animage.AnimageTextOverlay(drOverlay.Item("price").ToString.Trim, strPrefix & "PriceFont", Brushes.White, PriceRect, strPriceAlign))
                        End If
                        .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, LineRect, Pens.White))


                        'Add Bedrooms
                        Dim strBedsAlign As StringFormat = New StringFormat()
                        strBedsAlign.LineAlignment = StringAlignment.Center
                        strBedsAlign.Alignment = StringAlignment.Center

                        Dim strTitleAlign As StringFormat = New StringFormat()
                        strTitleAlign.LineAlignment = StringAlignment.Center
                        strTitleAlign.Alignment = StringAlignment.Near

                        Dim strPtTitleAlign As StringFormat = New StringFormat()
                        strPtTitleAlign.LineAlignment = StringAlignment.Center
                        strPtTitleAlign.Alignment = StringAlignment.Near

                        If drOverlay.Item("iscommercial") = 1 Then

                            Dim strCommercialTitle As String = ""

                            If drOverlay.Item("forsale") = 1 And drOverlay.Item("forrent") = 1 Then
                                strCommercialTitle = ""
                            ElseIf drOverlay.Item("forsale") = 1 Then
                                strCommercialTitle = "Commercial Property For Sale"
                            ElseIf drOverlay.Item("forrent") = 1 Then
                                strCommercialTitle = "Commercial Property To Let"
                            End If
                            'Add Commercial Image
                            .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strCommercialLogoName & ".png", CommercialLogoRect))

                            'Add Commercial title
                            .Add(New Animage.AnimageTextOverlay(strCommercialTitle, strPrefix & "CommercialTitle", Brushes.White, CommercialTitleRect, strPtTitleAlign))
                        Else
                            If drOverlay.Item("theme") = 1 Then '1 for Property24 
                                'Add bedrooms Image
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strThemeName & ".png", ThemeRect))

                                'Add Number of bedrooms
                                .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bedrooms").ToString.Trim, strPrefix & "PtNumFont", Brushes.White, PTNoBedroomRect, strBedsAlign))

                                'Add bedrooms title
                                .Add(New Animage.AnimageTextOverlay("Bedrooms", strPrefix & "PtTitle", Brushes.White, PTBedTitle, strPtTitleAlign))
                            Else '2 for Others
                                'Add bedrooms Image
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strBedroomName.ToString & ".png", BedroomRect))

                                'Add Number of bedrooms
                                .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bedrooms").ToString.Trim, strPrefix & "NumFont", Brushes.White, NoBedRoomRect, strBedsAlign))

                                'Add bedrooms title
                                .Add(New Animage.AnimageTextOverlay("BEDROOMS", strPrefix & "Title", Brushes.White, BedroomTitleRect, strTitleAlign))
                            End If

                            'Add Bathrooms
                            Dim strBathAlign As StringFormat = New StringFormat()
                            strBathAlign.LineAlignment = StringAlignment.Center
                            strBathAlign.Alignment = StringAlignment.Near

                            If drOverlay.Item("theme") = 1 Then '1 for Property24 

                                'Add bathrooms Image
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strThemeName & ".png", ThemeRect2))

                                'Add Number of bathrooms
                                .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bathrooms").ToString.Trim, strPrefix & "PtNumFont", Brushes.White, PtNoBathroomRect, strBedsAlign))

                                'Add bathrooms title
                                .Add(New Animage.AnimageTextOverlay("Bathrooms", strPrefix & "PtTitle", Brushes.White, PTBathTitle, strPtTitleAlign))

                            Else '2 for Others

                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strBathroomsName & ".png", BathroomRect))

                                'Add Number of bathrooms
                                .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bathrooms").ToString.Trim, strPrefix & "NumFont", Brushes.White, NoBathroomRect, strBedsAlign))

                                'Add bathrooms title
                                .Add(New Animage.AnimageTextOverlay("BATHROOMS", strPrefix & "Title", Brushes.White, BathroomTitleRect, strTitleAlign))
                            End If
                        End If

                        'Add Agent Logo
                        If Not RectF = Nothing Then
                            .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg", RectF))
                        End If
                    ElseIf slide = 1 Then ' Add Map Overlay

                        Dim intHeight As Integer = 0
                        Dim LocationTitleRect As RectangleF
                        Dim LocationRect As RectangleF

                        If blnMakeHDVideo Then
                            intHeight = 180
                            LocationTitleRect = New RectangleF(39, 48, 1240, 42)
                            LocationRect = New RectangleF(39, 115, 1240, 45)
                        Else
                            intHeight = 100
                            LocationTitleRect = New RectangleF(20, 20, 620, 30)
                            LocationRect = New RectangleF(20, 60, 620, 35)
                        End If

                        Dim strContact As String = ""
                        If Common.IsNull(drOverlay.Item("location")) Then
                            strContact = Common.ConvertNull(drOverlay.Item("location"), "")
                        Else
                            strContact = drOverlay.Item("location")
                            strContact = strContact.Trim
                        End If

                        If File.Exists(Application.StartupPath & "/Maps/" & intVideoID & ".jpg") Then
                            'Add Black Overlay
                            .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, New RectangleF(0, 0, VideoWidth, intHeight), Pens.Transparent, New SolidBrush(Color.FromArgb(200, 0, 0, 0))))

                            Dim strTitleAlign As StringFormat = New StringFormat()
                            strTitleAlign.LineAlignment = StringAlignment.Near
                            strTitleAlign.Alignment = StringAlignment.Near

                            .Add(New Animage.AnimageTextOverlay("LOCATED IN:", strPrefix & "Title", Brushes.White, LocationTitleRect, strTitleAlign))
                            .Add(New Animage.AnimageTextOverlay(strContact, strPrefix & "Location", Brushes.White, LocationRect, strTitleAlign))
                        Else
                            If Not RectF = Nothing Then
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg", RectF))
                            End If
                        End If

                    ElseIf slide = 2 Then 'clone map image
                        Dim intHeight As Integer = 0
                        Dim LocationTitleRect As RectangleF
                        Dim LocationRect As RectangleF

                        If blnMakeHDVideo Then
                            intHeight = 180
                            LocationTitleRect = New RectangleF(39, 48, 1240, 42)
                            LocationRect = New RectangleF(39, 115, 1240, 45)
                        Else
                            intHeight = 100
                            LocationTitleRect = New RectangleF(20, 20, 620, 30)
                            LocationRect = New RectangleF(20, 60, 620, 35)
                        End If

                        Dim strContact As String = ""
                        If Common.IsNull(drOverlay.Item("location")) Then
                            strContact = Common.ConvertNull(drOverlay.Item("location"), "")
                        Else
                            strContact = drOverlay.Item("location")
                            strContact = strContact.Trim
                        End If

                        If File.Exists(Application.StartupPath & "/Maps/" & intVideoID & ".jpg") Then
                            'Add Black Overlay
                            .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, New RectangleF(0, 0, VideoWidth, intHeight), Pens.Transparent, New SolidBrush(Color.FromArgb(200, 0, 0, 0))))

                            Dim strTitleAlign As StringFormat = New StringFormat()
                            strTitleAlign.LineAlignment = StringAlignment.Near
                            strTitleAlign.Alignment = StringAlignment.Near

                            .Add(New Animage.AnimageTextOverlay("LOCATED IN:", strPrefix & "Title", Brushes.White, LocationTitleRect, strTitleAlign))
                            .Add(New Animage.AnimageTextOverlay(strContact, strPrefix & "Location", Brushes.White, LocationRect, strTitleAlign))
                        Else
                            If Not RectF = Nothing Then
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg", RectF))
                            End If
                        End If
                    ElseIf slide = video.Slides.Count - 1 Then ' Last Slide

                    ElseIf slide = video.Slides.Count - 2 Then 'Second Last Slide for closing screen

                        If drOverlay.Item("isexistagent") = 1 Then

                            Dim ContactTitleRect As RectangleF
                            Dim OutroLineRect As RectangleF
                            If blnMakeHDVideo Then
                                ContactTitleRect = New RectangleF(69, 63, 691, 72)
                                OutroLineRect = New RectangleF(69, 174, 691, 1)
                                FIXED_AGENTPHOTO_HEIGHT = 230
                                FIXED_AGENTPHOTO_WIDTH = 165
                            Else
                                ContactTitleRect = New RectangleF(34, 40, 352, 45)
                                OutroLineRect = New RectangleF(34, 85, 352, 1)
                                FIXED_AGENTPHOTO_HEIGHT = 120
                                FIXED_AGENTPHOTO_WIDTH = 85
                            End If

                            ''Closing Screen Overlay For Non PT Client
                            .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, New RectangleF(0, 0, VideoWidth, VideoHeight), Pens.Transparent, New SolidBrush(Color.FromArgb(200, 0, 0, 0))))

                            'Add Contact Title
                            Dim strContactAlign As StringFormat = New StringFormat()
                            strContactAlign.LineAlignment = StringAlignment.Near
                            strContactAlign.Alignment = StringAlignment.Near
                            .Add(New Animage.AnimageTextOverlay("Contact The Agent", strPrefix & "ContactTitle", Brushes.White, ContactTitleRect, strContactAlign))

                            'Add White Line
                            .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, OutroLineRect, Pens.White))


                            'Resize Agent Photo

                            'Add agent Photo
                            Dim StartX As Double = 0
                            Dim StartY As Double = 0
                            If blnMakeHDVideo Then
                                StartX = 69
                                StartY = 268
                            Else
                                StartX = 34
                                StartY = 132
                            End If

                            If File.Exists(Application.StartupPath & "/AgentPhotos/" & intVideoID & ".jpg") Then

                                Dim AgentImage As Image = Nothing
                                AgentImage = Image.FromFile(Application.StartupPath & "/AgentPhotos/" & intVideoID & ".jpg")
                                If Not AgentImage Is Nothing Then

                                    Dim AX As Integer = 0
                                    Dim AY As Integer = 0
                                    Dim AHeight As Double = 0
                                    Dim AWidth As Double = 0

                                    If AgentImage.Height > AgentImage.Width Then
                                        AWidth = FIXED_AGENTPHOTO_WIDTH
                                        AHeight = FIXED_AGENTPHOTO_WIDTH * AgentImage.Height / AgentImage.Width

                                        If AHeight > FIXED_AGENTPHOTO_HEIGHT Then
                                            Dim New_Fixed_Agent_Width As Integer = FIXED_AGENTPHOTO_WIDTH
                                            Do While AHeight > FIXED_AGENTPHOTO_HEIGHT
                                                New_Fixed_Agent_Width = New_Fixed_Agent_Width - 20
                                                AHeight = New_Fixed_Agent_Width * AgentImage.Height / AgentImage.Width
                                            Loop
                                            AWidth = New_Fixed_Agent_Width
                                        End If
                                    Else
                                        AHeight = FIXED_AGENTPHOTO_HEIGHT
                                        AWidth = FIXED_AGENTPHOTO_HEIGHT * AgentImage.Width / AgentImage.Height

                                        If AWidth > FIXED_AGENTPHOTO_WIDTH Then
                                            Dim New_Fixed_Agent_Height As Integer = FIXED_AGENTPHOTO_HEIGHT
                                            Do While AWidth > FIXED_AGENTPHOTO_WIDTH
                                                New_Fixed_Agent_Height = New_Fixed_Agent_Height - 20
                                                AWidth = New_Fixed_Agent_Height * AgentImage.Width / AgentImage.Height
                                            Loop
                                            AHeight = New_Fixed_Agent_Height
                                        End If
                                    End If

                                    AX = StartX  'Starting from Left
                                    AY = StartY 'Top marging

                                    WriteToAppLog("Agent Photo Position - X:" & AX & " Y:" & AY & " Width:" & AWidth & "  Height:" & AHeight)
                                    .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/AgentPhotos/" & intVideoID & ".jpg", New RectangleF(AX, AY, AWidth, AHeight)))

                                    If blnMakeHDVideo Then
                                        StartX += AWidth + 30 ' 30px Margin after Agent Photo for dispaly agent contact information
                                    Else
                                        StartX += AWidth + 20 ' 20px Margin after Agent Photo for dispaly agent contact information
                                    End If
                                Else
                                    Throw New Exception("Video ID - " & intVideoID & " : AgentPhoto image were corrupted")
                                End If
                            End If


                            'Add Agent Name
                            Dim AgentNameRect As RectangleF

                            If blnMakeHDVideo Then
                                AgentNameRect = New RectangleF(StartX, 268, 800, 60)
                            Else
                                AgentNameRect = New RectangleF(StartX, 134, 400, 32)
                            End If

                            Dim strAgentNameAlign As StringFormat = New StringFormat()
                            strAgentNameAlign.LineAlignment = StringAlignment.Near
                            strAgentNameAlign.Alignment = StringAlignment.Near
                            If Not ConvertNull(drOverlay.Item("name")) = "" Then
                                .Add(New Animage.AnimageTextOverlay(drOverlay.Item("name").ToString.Trim, strPrefix & "AgentName", Brushes.White, AgentNameRect, strAgentNameAlign))
                                If blnMakeHDVideo Then
                                    StartY += 60 + 2 '2px For Margin 
                                Else
                                    StartY += 32 + 5 '2px For Margin 
                                End If
                            End If

                            Dim strContactInfoAlign As StringFormat = New StringFormat()
                            strContactInfoAlign.LineAlignment = StringAlignment.Near
                            strContactInfoAlign.Alignment = StringAlignment.Near
                            'Add agent Cell No
                            Dim CellRect As RectangleF
                            If blnMakeHDVideo Then
                                CellRect = New RectangleF(StartX, StartY, 485, 35)
                            Else
                                CellRect = New RectangleF(StartX, StartY, 245, 20)
                            End If
                            If Not ConvertNull(drOverlay.Item("mobile")) = "" Then
                                Dim CellInfo As String = "Cell: "
                                CellInfo += drOverlay.Item("mobile")
                                .Add(New Animage.AnimageTextOverlay(CellInfo, strPrefix & "ContactInfo", Brushes.White, CellRect, strContactInfoAlign))
                                If blnMakeHDVideo Then
                                    StartY += 35 + 2 '2px For Margin
                                Else
                                    StartY += 20 + 2 '2px For Margin
                                End If
                            End If

                            'Add agent office No
                            Dim TeleRect As RectangleF
                            If blnMakeHDVideo Then
                                TeleRect = New RectangleF(StartX, StartY, 485, 35)
                            Else
                                TeleRect = New RectangleF(StartX, StartY, 245, 20)
                            End If
                            If Not ConvertNull(drOverlay.Item("officenumber")) = "" Then
                                Dim TelInfo As String = "Tel: "
                                TelInfo += drOverlay.Item("officenumber")
                                .Add(New Animage.AnimageTextOverlay(TelInfo, strPrefix & "ContactInfo", Brushes.White, TeleRect, strContactInfoAlign))
                                If blnMakeHDVideo Then
                                    StartY += 35 + 2 '2px For Margin
                                Else
                                    StartY += 20 + 2 '2px For Margin
                                End If
                            End If

                            'Add agent Email Info
                            Dim EmailRect As RectangleF
                            If blnMakeHDVideo Then
                                EmailRect = New RectangleF(StartX, StartY, 800, 40)
                            Else
                                EmailRect = New RectangleF(StartX, StartY, 400, 25)
                            End If
                            If Not ConvertNull(drOverlay.Item("email")) = "" Then
                                Dim EmailInfo As String = "Email: "
                                EmailInfo += ConvertNull(drOverlay.Item("email"), "")
                                .Add(New Animage.AnimageTextOverlay(EmailInfo, strPrefix & "ContactInfo", Brushes.White, EmailRect, strContactInfoAlign))
                            End If

                            'Add Agent Logo
                            If Not RectF = Nothing Then
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg", RectF))
                            End If
                        Else
                            ''Closing Screen Overlay For  PT24Client
                            'Add Black Overlay
                            .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, New RectangleF(0, 0, VideoWidth, VideoHeight), Pens.Transparent, New SolidBrush(Color.FromArgb(200, 0, 0, 0))))

                            'Add Price
                            Dim strPriceAlign As StringFormat = New StringFormat()
                            strPriceAlign.LineAlignment = StringAlignment.Near
                            strPriceAlign.Alignment = StringAlignment.Near
                            If drOverlay.Item("theme") = 1 Then
                                .Add(New Animage.AnimageTextOverlay(drOverlay.Item("price").ToString.Trim, strPrefix & "PtPriceFont", Brushes.White, PriceRect, strPriceAlign))
                            Else
                                .Add(New Animage.AnimageTextOverlay(drOverlay.Item("price").ToString.Trim, strPrefix & "PriceFont", Brushes.White, PriceRect, strPriceAlign))
                            End If
                            .Add(New Animage.AnimageShapeOverlay(Animage.Animage.AnimageShape.Rectangle, LineRect, Pens.White))


                            'Add Bedrooms
                            Dim strBedsAlign As StringFormat = New StringFormat()
                            strBedsAlign.LineAlignment = StringAlignment.Center
                            strBedsAlign.Alignment = StringAlignment.Center

                            Dim strTitleAlign As StringFormat = New StringFormat()
                            strTitleAlign.LineAlignment = StringAlignment.Center
                            strTitleAlign.Alignment = StringAlignment.Near

                            Dim strPtTitleAlign As StringFormat = New StringFormat()
                            strPtTitleAlign.LineAlignment = StringAlignment.Center
                            strPtTitleAlign.Alignment = StringAlignment.Near

                            If drOverlay.Item("iscommercial") = 1 Then

                                Dim strCommercialTitle As String = ""

                                If drOverlay.Item("forsale") = 1 And drOverlay.Item("forrent") = 1 Then
                                    strCommercialTitle = ""
                                ElseIf drOverlay.Item("forsale") = 1 Then
                                    strCommercialTitle = "Commercial Property For Sale"
                                ElseIf drOverlay.Item("forrent") = 1 Then
                                    strCommercialTitle = "Commercial Property To Let"
                                End If
                                'Add Commercial Image
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strCommercialLogoName & ".png", CommercialLogoRect))

                                'Add Commercial title
                                .Add(New Animage.AnimageTextOverlay(strCommercialTitle, strPrefix & "CommercialTitle", Brushes.White, CommercialTitleRect, strPtTitleAlign))
                            Else
                                If drOverlay.Item("theme") = 1 Then '1 for Property24 
                                    'Add bedrooms Image
                                    .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strThemeName & ".png", ThemeRect))

                                    'Add Number of bedrooms
                                    .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bedrooms").ToString.Trim, strPrefix & "PtNumFont", Brushes.White, PTNoBedroomRect, strBedsAlign))

                                    'Add bedrooms title
                                    .Add(New Animage.AnimageTextOverlay("Bedrooms", strPrefix & "PtTitle", Brushes.White, PTBedTitle, strPtTitleAlign))
                                Else '2 for Others
                                    'Add bedrooms Image
                                    .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strBedroomName.ToString & ".png", BedroomRect))

                                    'Add Number of bedrooms
                                    .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bedrooms").ToString.Trim, strPrefix & "NumFont", Brushes.White, NoBedRoomRect, strBedsAlign))

                                    'Add bedrooms title
                                    .Add(New Animage.AnimageTextOverlay("BEDROOMS", strPrefix & "Title", Brushes.White, BedroomTitleRect, strTitleAlign))
                                End If

                                'Add Bathrooms
                                Dim strBathAlign As StringFormat = New StringFormat()
                                strBathAlign.LineAlignment = StringAlignment.Center
                                strBathAlign.Alignment = StringAlignment.Near

                                If drOverlay.Item("theme") = 1 Then '1 for Property24 

                                    'Add bathrooms Image
                                    .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strThemeName & ".png", ThemeRect2))

                                    'Add Number of bathrooms
                                    .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bathrooms").ToString.Trim, strPrefix & "PtNumFont", Brushes.White, PtNoBathroomRect, strBedsAlign))

                                    'Add bathrooms title
                                    .Add(New Animage.AnimageTextOverlay("Bathrooms", strPrefix & "PtTitle", Brushes.White, PTBathTitle, strPtTitleAlign))

                                Else '2 for Others

                                    .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/resources/" & strBathroomsName & ".png", BathroomRect))

                                    'Add Number of bathrooms
                                    .Add(New Animage.AnimageTextOverlay(drOverlay.Item("bathrooms").ToString.Trim, strPrefix & "NumFont", Brushes.White, NoBathroomRect, strBedsAlign))

                                    'Add bathrooms title
                                    .Add(New Animage.AnimageTextOverlay("BATHROOMS", strPrefix & "Title", Brushes.White, BathroomTitleRect, strTitleAlign))
                                End If
                            End If


                            'Add Agent Logo
                            If Not RectF = Nothing Then
                                .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg", RectF))
                            End If
                        End If
                    Else 'Other Slides

                        If Not RectF = Nothing Then
                            .Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg", RectF))
                        End If

                    End If
                End With
            Next
            AddOverlays = True
        Catch ex As Exception
            WriteToAppLog("AddOverlays Function : " & ex.ToString)
            WriteToErrorLog("AddOverlays Function : " & ex.ToString)
            AddOverlays = False
        End Try
        If conOverlays IsNot Nothing Then conOverlays.Close() : conOverlays.Dispose()
    End Function

    Private Function GetLogoDimension() As RectangleF
        Dim Rect As New RectangleF
        Try
            'Add Agent Logo
            Dim intMarginFromBottom As Integer = 0
            If blnMakeHDVideo Then
                FIXED_LOGO_HEIGHT = 150
                FIXED_LOGO_WIDTH = 150
                intMarginFromBottom = 30
            Else
                FIXED_LOGO_HEIGHT = 85
                FIXED_LOGO_WIDTH = 85
                intMarginFromBottom = 20
            End If
            Dim OverlayImage As Image = Nothing
            OverlayImage = Image.FromFile(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg")
            If Not OverlayImage Is Nothing Then
                Dim OX As Integer = 0
                Dim OY As Integer = 0
                Dim OHeight As Double = 0
                Dim OWidth As Double = 0

                ''If OverlayImage.Height > OverlayImage.Width Then
                ''    OWidth = FIXED_LOGO_WIDTH
                ''    OHeight = FIXED_LOGO_WIDTH * OverlayImage.Height / OverlayImage.Width
                ''Else
                ''    OHeight = FIXED_LOGO_HEIGHT
                ''    OWidth = FIXED_LOGO_HEIGHT * OverlayImage.Width / OverlayImage.Height

                ''    If OWidth > LOGO_BOUND_WIDTH Then
                ''        Dim New_Fixed_Logo_Height As Integer = FIXED_LOGO_HEIGHT
                ''        Do While OWidth > LOGO_BOUND_WIDTH
                ''            New_Fixed_Logo_Height = New_Fixed_Logo_Height - 20
                ''            OWidth = New_Fixed_Logo_Height * OverlayImage.Width / OverlayImage.Height
                ''        Loop
                ''        OHeight = New_Fixed_Logo_Height
                ''    End If
                ''End If
                'OX = 762 + ((520 - OWidth) / 2)  'Center Logo
                'OY = VideoHeight - OHeight - 30  '63 'Top marging

                If OverlayImage.Height > OverlayImage.Width Then
                    OWidth = FIXED_LOGO_WIDTH
                    OHeight = FIXED_LOGO_WIDTH * OverlayImage.Height / OverlayImage.Width
                Else
                    OHeight = FIXED_LOGO_HEIGHT
                    OWidth = FIXED_LOGO_HEIGHT * OverlayImage.Width / OverlayImage.Height
                End If


                Dim intPercentage As Integer = 40 '30 - 10% increase as per client requirement 29/01/2015
                'If blnMakeHDVideo Then
                '    intPercentage = 30
                'Else
                '    intPercentage = 15
                'End If

                OWidth = OWidth - (OWidth * intPercentage / 100)
                OHeight = OHeight - (OHeight * intPercentage / 100)

                OX = VideoWidth - OWidth - 20  '20 pixel margin from right
                OY = VideoHeight - OHeight - intMarginFromBottom '20 pixel margin from Bottom

                Rect = New RectangleF(OX, OY, OWidth, OHeight)
                'WriteToAppLog("Logo Position - X:" & OX & " Y:" & OY & " Width:" & OWidth & "  Height:" & OHeight)
                '.Add(New Animage.AnimageImageOverlay(Application.StartupPath & "/OverlayImages/" & intVideoID & ".jpg", New RectangleF(OX, OY, OWidth, OHeight)))
            Else
                Throw New Exception("Video ID - " & intVideoID & " : Overlay image were corrupted")
            End If
        Catch ex As Exception
            Rect = Nothing
            WriteToAppLog("GetLogoDimension Function - Error - " & ex.ToString)
            WriteToErrorLog("GetLogoDimension Function - Error - " & ex.ToString)
        End Try
        Return Rect
    End Function

    Private Function GenerateNarration(ByVal VoiceType As Integer) As Integer

        Dim conNarration As MySqlConnection = Nothing
        Dim intNarrationTime As Integer = 0
        Dim drFullDescription As DataTableReader = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conNarration = Database.Connection.Clone
            If Not conNarration.State = ConnectionState.Open Then Call conNarration.Open()


            If Not Directory.Exists(Application.StartupPath & "/Narration/" & intVideoID) Then
                Directory.CreateDirectory(Application.StartupPath & "/Narration/" & intVideoID)
            End If

            drFullDescription = Database.FetchDataReader("SELECT videos.spokentitle, videos.youtubetitle, propertydetails.fulldescription FROM propertydetails,videos WHERE videos.videoid=propertydetails.videoid and videos.videoid=" & intVideoID, Nothing, conNarration)
            If drFullDescription.Read Then
                If Not ConvertNull(drFullDescription.Item("fulldescription")) = "" Then
                    Try
                        Dim intConvertRetry As Integer = 0
                        Dim intDownloadRetry As Integer = 0
                        Dim sb As New StringBuilder
                        sb.Append("Welcome to this, ")
                        If Not drFullDescription.Item("spokentitle") = "" Then
                            sb.Append(ConvertNull(drFullDescription.Item("spokentitle")) & " ")
                        Else
                            sb.Append(ConvertNull(drFullDescription.Item("youtubetitle")) & " ")
                        End If
                        sb.Append("!")
                        sb.Append(ConvertNull(drFullDescription.Item("fulldescription")))
                        sb.Append("!")
                        sb.Append(" For more information on this property or to arrange a viewing please contact us.")

                        Dim strVoiceName As String = ""
                        If VoiceType = Voices.Kate Then
                            strVoiceName = "TTS_KATE_DB"
                        Else
                            strVoiceName = "TTS_NEOBRIDGET_DB"
                        End If



                        Do While intConvertRetry < 2
                            Dim objVoice As NeoSpeechTtsSoapService = New NeoSpeechTtsSoapService()
                            Try
                                Dim strConvertText As String() = objVoice.ConvertText(NeoSpeechEmail, NeoSpeechAccountId, NeoSpeechLoginKey, NeoSpeechLoginPwd, strVoiceName, "FORMAT_WAV", 16, sb.ToString(), True, 50, 100, 100)
                                'strConvertText(0) =  resultCode,resultString,conversionNumber,status,statusCode
                                'strConvertText(1) =  resultCode
                                'strConvertText(2) =  resultString
                                'strConvertText(3) =  conversionNumber
                                'strConvertText(4) = status
                                'strConvertText(5) =  statusCode
                                If (strConvertText(2) = "success") Then
                                    Do While intDownloadRetry < 3
                                        Try
                                            Dim URLResponse As String() = objVoice.GetConversionStatus(NeoSpeechEmail, NeoSpeechAccountId, Integer.Parse(strConvertText(3)))
                                            'URLResponse(0) =  resultCode,resultString,status,statusCode,downloadURL
                                            'URLResponse(1) =  resultCode
                                            'URLResponse(2) =  resultString
                                            'URLResponse(3) =  status
                                            'URLResponse(4) = statusCode
                                            'URLResponse(5) =  downloadURL

                                            If URLResponse(3) = "Completed" Then
                                                Try

                                                    Dim myWebClient As New WebClient
                                                    myWebClient.DownloadFile(URLResponse(5).ToString(), Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav")
                                                    WriteToAppLog("Video ID - " & intVideoID & " : GenerateNarration Function.. Narration file downloaded Succesfully")


                                                    'Convert Wav file into MP3
                                                    If File.Exists(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav") Then
                                                        Dim strInputFilePath As String = Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav"
                                                        Dim strOutputFilePath As String = Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3"
                                                        Dim WavToMp3Process As New Process
                                                        Try
                                                            Dim result As String = ""
                                                            Dim errorreader As StreamReader = Nothing

                                                            WavToMp3Process.StartInfo.UseShellExecute = False
                                                            WavToMp3Process.StartInfo.ErrorDialog = False
                                                            WavToMp3Process.StartInfo.RedirectStandardError = True
                                                            WavToMp3Process.StartInfo.CreateNoWindow = True
                                                            WavToMp3Process.StartInfo.FileName = """" & Application.StartupPath & "\libs\ffmpeg_new\ffmpeg.exe" & """"
                                                            WavToMp3Process.StartInfo.Arguments = " -y -i " & """" & strInputFilePath & """" & " -f mp3 " & """" & strOutputFilePath & """"
                                                            WavToMp3Process.Start()
                                                            errorreader = WavToMp3Process.StandardError
                                                            result = errorreader.ReadToEnd()
                                                            WavToMp3Process.WaitForExit()
                                                            WriteToAppLog("Video ID - " & intVideoID & " : Narration Converted Successfully")
                                                        Catch ex As Exception
                                                            Throw New Exception("Video ID - " & intVideoID & " : Narration Wav to Mp3 Conversion Failed")
                                                        End Try
                                                        WavToMp3Process.Close()
                                                        WavToMp3Process.Dispose()
                                                    Else
                                                        Throw New Exception("Video ID - " & intVideoID & " : Narration Wav file not generated")
                                                    End If

                                                    Exit Do
                                                Catch ex As Exception
                                                    WriteToAppLog("Video ID - " & intVideoID & " : GenerateNarration Function... Download Narration Error.... " & ex.ToString())
                                                End Try
                                            Else
                                                WriteToAppLog("Video ID - " & intVideoID & " : GenerateNarration Function... Download Narration Response is Status - " + URLResponse(3).ToString())
                                                Thread.Sleep(New TimeSpan(0, 0, 5))
                                            End If

                                        Catch ex As Exception
                                            WriteToAppLog("Video ID - " & intVideoID & " : GenerateNarration Function... GetConversionStatus Error - " + ex.ToString())
                                        End Try
                                        intDownloadRetry += 1
                                    Loop
                                    Exit Do
                                Else
                                    WriteToAppLog("Video ID - " & intVideoID & " : GenerateNarration Function... Convert Text Narration Response is Result string - " + strConvertText(2).ToString())
                                End If

                            Catch ex As Exception
                                WriteToAppLog("Video ID - " & intVideoID & " : GenerateNarration Function... Convert Text error.. " & ex.ToString())
                            End Try
                            intConvertRetry += 1
                        Loop
                    Catch ex As Exception
                        WriteToAppLog("Video ID - " & intVideoID & " : GenerateNarration Function... Narration Settings error.. " & ex.ToString())
                    End Try
                    'Get length of narration
                    If File.Exists(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3") Then
                        intNarrationTime = GetNarrationDuration(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3")
                        WriteToAppLog("Video ID - " & intVideoID & " : Successfully Get Narration Duration")
                        '  Delete Narration Wav file
                        If File.Exists(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav") Then
                            File.Delete(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav")
                        End If
                    End If

                Else
                    Throw New Exception("Video ID - " & intVideoID & " : Full Description is blank in Property Details")
                End If
            Else
                Throw New Exception("Video ID - " & intVideoID & " : Undefine Full Description in Property Details")
            End If
        Catch ex As Exception
            WriteToAppLog("GenerateNarration Function - Error - " & ex.ToString)
            WriteToErrorLog("GenerateNarration Function - Error - " & ex.ToString)
        End Try
        If Not drFullDescription Is Nothing Then drFullDescription.Close()
        If conNarration IsNot Nothing Then conNarration.Close() : conNarration.Dispose()
        Return intNarrationTime
    End Function

    'Private Function GenerateNarration_IVONA(ByVal VoiceType As Integer) As Integer
    '    Dim conNarration As MySqlConnection = Nothing
    '    Dim intNarrationTime As Integer = 0
    '    Dim drFullDescription As DataTableReader = Nothing
    '    Try
    '        If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
    '        conNarration = Database.Connection.Clone
    '        If Not conNarration.State = ConnectionState.Open Then Call conNarration.Open()


    '        If Not Directory.Exists(Application.StartupPath & "/Narration/" & intVideoID) Then
    '            Directory.CreateDirectory(Application.StartupPath & "/Narration/" & intVideoID)
    '        End If

    '        drFullDescription = Database.FetchDataReader("SELECT videos.spokentitle, videos.youtubetitle, propertydetails.fulldescription FROM propertydetails,videos WHERE videos.videoid=propertydetails.videoid and videos.videoid=" & intVideoID, Nothing, conNarration)
    '        If drFullDescription.Read Then
    '            If Not ConvertNull(drFullDescription.Item("fulldescription")) = "" Then

    '                ''Code for Create Narration Using Speech Synthesizer Using SpeakSsml Method
    '                Dim objSynth As New SpeechSynthesizer
    '                Try
    '                    Dim sb As New StringBuilder
    '                    sb.Append("<?xml version='1.0'?> ")
    '                    sb.Append("<speak version='1.0' ")
    '                    sb.Append("xml:lang='en-US'>")

    '                    If VoiceType = Voices.Salli Then
    '                        sb.Append("<voice name='IVONA 2 Salli'>")
    '                    Else
    '                        sb.Append("<voice name='IVONA 2 Amy'>")
    '                    End If
    '                    sb.Append("Welcome to this ")

    '                    If Not drFullDescription.Item("spokentitle") = "" Then
    '                        sb.Append(ConvertNull(drFullDescription.Item("spokentitle")) & " ")
    '                    Else
    '                        sb.Append(ConvertNull(drFullDescription.Item("youtubetitle")) & " ")
    '                    End If

    '                    sb.Append("<break time='3s' />")
    '                    sb.Append(ConvertNull(drFullDescription.Item("fulldescription")))
    '                    sb.Append("<break time='3s' />")
    '                    sb.Append(" For more information on this property or to arrange a viewing please contact us.")
    '                    sb.Append("</voice>")
    '                    sb.Append("</speak>")

    '                    objSynth.Volume = 100
    '                    objSynth.SetOutputToWaveFile(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav")
    '                    objSynth.SpeakSsml(sb.ToString)
    '                    WriteToAppLog("Video ID - " & intVideoID & " : Narration Created Successfully")
    '                Catch ex As Exception
    '                    Throw New Exception("Video ID - " & intVideoID & " : Narration Generation Failed - Error : " & ex.ToString)
    '                End Try
    '                objSynth.Dispose()

    '                'Convert Wav file into MP3
    '                If File.Exists(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav") Then
    '                    Dim strInputFilePath As String = Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav"
    '                    Dim strOutputFilePath As String = Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3"
    '                    Dim WavToMp3Process As New Process
    '                    Try
    '                        Dim result As String = ""
    '                        Dim errorreader As StreamReader = Nothing

    '                        WavToMp3Process.StartInfo.UseShellExecute = False
    '                        WavToMp3Process.StartInfo.ErrorDialog = False
    '                        WavToMp3Process.StartInfo.RedirectStandardError = True
    '                        WavToMp3Process.StartInfo.CreateNoWindow = True
    '                        WavToMp3Process.StartInfo.FileName = """" & Application.StartupPath & "\libs\ffmpeg_new\ffmpeg.exe" & """"
    '                        WavToMp3Process.StartInfo.Arguments = " -y -i " & """" & strInputFilePath & """" & " -f mp3 " & """" & strOutputFilePath & """"
    '                        WavToMp3Process.Start()
    '                        errorreader = WavToMp3Process.StandardError
    '                        result = errorreader.ReadToEnd()
    '                        WavToMp3Process.WaitForExit()
    '                        WriteToAppLog("Video ID - " & intVideoID & " : Narration Converted Successfully")
    '                    Catch ex As Exception
    '                        Throw New Exception("Video ID - " & intVideoID & " : Narration Wav to Mp3 Conversion Failed")
    '                    End Try
    '                    WavToMp3Process.Close()
    '                    WavToMp3Process.Dispose()
    '                Else
    '                    Throw New Exception("Video ID - " & intVideoID & " : Narration Wav file not generated")
    '                End If

    '                'Get length of narration
    '                If File.Exists(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3") Then
    '                    intNarrationTime = GetNarrationDuration(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3")
    '                    WriteToAppLog("Video ID - " & intVideoID & " : Successfully Get Narration Duration")
    '                    'Delete Narration Wav file
    '                    If File.Exists(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav") Then
    '                        File.Delete(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".wav")
    '                    End If
    '                End If
    '            Else
    '                Throw New Exception("Video ID - " & intVideoID & " : Full Description is blank in Property Details")
    '            End If
    '        Else
    '            Throw New Exception("Video ID - " & intVideoID & " : Undefine Full Description in Property Details")
    '        End If
    '    Catch ex As Exception
    '        WriteToAppLog("GenerateNarration Function - Error - " & ex.ToString)
    '        WriteToErrorLog("GenerateNarration Function - Error - " & ex.ToString)
    '    End Try
    '    If Not drFullDescription Is Nothing Then drFullDescription.Close()
    '    If conNarration IsNot Nothing Then conNarration.Close() : conNarration.Dispose()
    '    Return intNarrationTime
    'End Function

    Private Function GetNarrationDuration(ByVal InputFile As String) As Integer
        Dim intDuration As Integer = 0
        Dim DurationProcess As New Process
        Try
            Dim duration As String
            Dim result As String
            Dim errorreader As StreamReader
            DurationProcess.StartInfo.UseShellExecute = False
            DurationProcess.StartInfo.ErrorDialog = False
            DurationProcess.StartInfo.RedirectStandardError = True
            DurationProcess.StartInfo.CreateNoWindow = True
            DurationProcess.StartInfo.FileName = """" & Application.StartupPath & "\libs\ffmpeg_new\ffmpeg.exe" & """"
            DurationProcess.StartInfo.Arguments = "-i " & """" & InputFile & """"
            DurationProcess.Start()
            errorreader = DurationProcess.StandardError
            DurationProcess.WaitForExit()
            result = errorreader.ReadToEnd()
            duration = result.Substring(result.IndexOf("Duration: ") + ("Duration: ").Length, ("00:00:00.00").Length)
            Dim arr As Array = duration.Split(":")

            If arr(2).ToString.Contains(".") Then
                Dim fileDur As Integer = Fix(arr(2)) + 1
                intDuration = Math.Round((arr(0) * 3600) + (arr(1) * 60) + fileDur)
            Else
                intDuration = Math.Round((arr(0) * 3600) + (arr(1) * 60) + arr(2))
            End If

            DurationProcess.Close()
            DurationProcess.Dispose()
        Catch ex As Exception
            WriteToAppLog("GetNarrationDuration Function - Error - " & ex.ToString)
            WriteToErrorLog("GetNarrationDuration Function - Error - " & ex.ToString)
        End Try
        Return intDuration
    End Function

    Private Function GetFinalMusic() As String
        Try
            'Dim VideoSeconds As Integer = video.Slides.TotalFrames / video.FPS
            'Dim TotalVideoSecond As Integer = video.Slides.TotalFrames / video.FPS
            'Dim index As Integer = 1
            'Do While TotalVideoSecond > 0
            '    Dim musicpath As String = Application.StartupPath & "/Musics/" & GetMusic()
            '    Call audio.Pool.Add(musicpath, True, MusicVolume)
            '    With audio.Pool(index - 1)
            '        Dim AudioSeconds As Integer = .AudioTimeSpan.TotalSeconds
            '        If TotalVideoSecond > AudioSeconds Then
            '            .Fade = New EasyAudio.ParamFade(2, AudioSeconds, 2)
            '            .Trim = New EasyAudio.ParamTrim(0, AudioSeconds)
            '        Else
            '            .Fade = New EasyAudio.ParamFade(2, TotalVideoSecond, 2)
            '            .Trim = New EasyAudio.ParamTrim(0, TotalVideoSecond)
            '            .Pad = New EasyAudio.ParamPad(0, 0)
            '        End If
            '        TotalVideoSecond = TotalVideoSecond - AudioSeconds
            '    End With
            '    index += 1
            'Loop
            'Dim intTempVideoSeconds As Integer = 0

            Dim NextPadAudioStartDur As Integer = 0
            Dim TotalVideoSecond1 As Integer = video.Slides.TotalFrames / video.FPS
            Dim TotalVideoSecond As Integer = video.Slides.TotalFrames / video.FPS

            Dim index As Integer = 1
            Do While TotalVideoSecond > 0
                Dim musicpath As String = Application.StartupPath & "/Musics/" & GetMusic()
                Call audio.Pool.Add(musicpath, True, MusicVolume)
                With audio.Pool(index - 1)
                    Dim AudioSeconds As Integer = .AudioTimeSpan.TotalSeconds
                    If TotalVideoSecond > AudioSeconds Then
                        .Fade = New EasyAudio.ParamFade(2, AudioSeconds, 2)
                        .Trim = New EasyAudio.ParamTrim(0, AudioSeconds)

                        If index = 1 Then
                            .Pad = New EasyAudio.ParamPad(0, 0)
                        Else
                            .Pad = New EasyAudio.ParamPad(NextPadAudioStartDur, TotalVideoSecond1 - (NextPadAudioStartDur + AudioSeconds))
                        End If
                    Else
                        .Fade = New EasyAudio.ParamFade(2, TotalVideoSecond, 2)
                        .Trim = New EasyAudio.ParamTrim(0, TotalVideoSecond)
                        .Pad = New EasyAudio.ParamPad(NextPadAudioStartDur, 0)
                    End If
                    TotalVideoSecond = TotalVideoSecond - AudioSeconds
                    NextPadAudioStartDur += AudioSeconds
                End With
                index += 1
            Loop

            If intNarrationDuration > 0 Then
                If intVoiceOver = Voices.Kate Then
                    Call audio.Pool.Add(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3", True, (1.8))
                Else
                    Call audio.Pool.Add(Application.StartupPath & "/Narration/" & intVideoID & "/" & intVideoID & ".mp3", True, (NarrationVolume))
                End If

                With audio.Pool(index - 1)
                    .Trim = New EasyAudio.ParamTrim(0, intNarrationDuration)
                    .Pad = New EasyAudio.ParamPad(0, (TotalVideoSecond1 - intNarrationDuration))
                End With
            End If

            Dim FinalMusicPath As String = audio.Compile()
            Return FinalMusicPath
        Catch ex As Exception
            UpdateVideoError(ex.ToString, Me.intVideoID)
            WriteToErrorLog("GetFinalMusic Procedure : " & ex.ToString)
            Return vbNullString
        End Try
    End Function

    Private Function GetMusic() As String
        Dim conMusic As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conMusic = Database.Connection.Clone
            If Not conMusic.State = ConnectionState.Open Then Call conMusic.Open()
            Dim TotalMusicCount As Integer = Database.ExecuteSQL("select count(*) from musics", Nothing, conMusic)
            Dim random As New Random
            Dim randomNo As Integer = random.Next(1, TotalMusicCount + 1)
            Dim MusicPath As String = Database.ExecuteSQL("select musicpath from musics where musicid=" & randomNo, Nothing, conMusic)
            Return MusicPath
        Catch ex As Exception
            UpdateVideoError(ex.ToString, Me.intVideoID)
            WriteToErrorLog("GetMusic Procedure : " & ex.ToString)
            Return vbNullString
        End Try
        If conMusic IsNot Nothing Then conMusic.Close() : conMusic.Dispose()
    End Function

    Private Sub CheckWidth(ByVal imagewidth As Double, ByVal h1 As Double)
        Try
            'Dim w1 As Double = (h1 * 1080) / 720
            Dim w1 As Double = (h1 * VideoWidth) / VideoHeight
            If imagewidth < w1 Then
                ResizeY = (h1 * 20 / 100) / 2
                h1 = h1 - (h1 * 20 / 100)
                CheckWidth(imagewidth, h1)
            Else
                ResizeY = (h1 * 20 / 100) / 2
                HResizedHeight = h1
                HResizedWidth = w1
                Exit Sub
            End If
        Catch ex As Exception
            UpdateVideoError(ex.ToString, Me.intVideoID)
            WriteToErrorLog("CheckWidth Procedure : " & ex.ToString)
        End Try
    End Sub

    Private Sub CheckHeight(ByVal imageHeight As Double, ByVal w1 As Double)
        Try
            'Dim h1 As Double = (w1 * 720) / 1080
            Dim h1 As Double = (w1 * VideoHeight) / VideoWidth
            If imageHeight < h1 Then
                ResizeX = (w1 * 20 / 100) / 2
                w1 = w1 - (w1 * 20 / 100)
                CheckHeight(imageHeight, w1)
            Else
                VResizedHeight = h1
                VResizedWidth = w1
                ResizeX = (w1 * 20 / 100) / 2
                Exit Sub
            End If
        Catch ex As Exception
            UpdateVideoError(ex.ToString, Me.intVideoID)
            WriteToErrorLog("CheckHeight Procedure : " & ex.ToString)
        End Try
    End Sub

    Private Sub UpdateVideoDatabase(ByVal strVideopath As String, ByVal TotalSeconds As Integer, ByVal TotalImagesCount As Integer, ByVal Mp4VideoLength As Long)
        Dim conUpdateData As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conUpdateData = Database.Connection.Clone
            If Not conUpdateData.State = ConnectionState.Open Then Call conUpdateData.Open()
            Dim strError As String = ""

            Dim TotalVideoSecond As Integer = video.Slides.TotalFrames / video.FPS

            Dim comUpdatefsdData As New MySqlCommand("update videos set iscompiled=?iscompiled, compiledon=?compiledon,videopath=?videopath, compiletime=?compiletime, totalimages=?totalimages, videotime=?videotime, mp4videolength=?mp4videolength where videoid=?videoid", conUpdateData)
            With comUpdatefsdData
                With .Parameters
                    Call .AddWithValue("iscompiled", 1)
                    Call .AddWithValue("compiledon", Format(Now, "yyyy-MM-dd HH:mm:ss"))
                    Call .AddWithValue("videopath", strVideopath)
                    Call .AddWithValue("compiletime", TotalSeconds)
                    Call .AddWithValue("totalimages", TotalImagesCount)
                    Call .AddWithValue("videotime", TotalVideoSecond)
                    Call .AddWithValue("mp4videolength", Mp4VideoLength)
                    Call .AddWithValue("videoid", Me.intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With

        Catch ex As Exception
            UpdateVideoError(ex.ToString, Me.intVideoID)
            WriteToErrorLog("UpdateVideoDatabase Procedure : " & ex.ToString)
        End Try
        If conUpdateData IsNot Nothing Then conUpdateData.Close() : conUpdateData.Dispose()
    End Sub

    Private Sub UpdateVideoError(ByVal strerror As String, ByVal videoid As Integer)
        If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
        Dim conVideoError As MySqlConnection = Database.Connection.Clone
        If Not conVideoError.State = ConnectionState.Open Then Call conVideoError.Open()

        Dim intIsCompiled As Integer = -1
        Dim intUpload As Integer = -1
        Dim intIsYoutube As Integer = -1
        Dim intIsMp4VideoUploaded As Integer = -1
        Dim intIswebmvideo As Integer = -1
        Dim intIsWebmVideoUploaded As Integer = -1
        Dim intIssmartphonevideo As Integer = -1
        Dim intIsSmartphoneVideoUploaded As Integer = -1
        Dim intIsuploadmp4thumb As Integer = -1
        Dim intIsuploadsmartphonethumb As Integer = -1

        If strerror.StartsWith("System.Exception: Image Not Found in Directory") Then
            intIsCompiled = 2
            intUpload = 2
            intIsYoutube = 2
            intIsMp4VideoUploaded = 2
            intIswebmvideo = 2
            intIsWebmVideoUploaded = 2
            intIssmartphonevideo = 2
            intIsSmartphoneVideoUploaded = 2
            intIsuploadmp4thumb = 2
            intIsuploadsmartphonethumb = 2
        ElseIf strerror.StartsWith("System.Exception: Video Compiling Failed because Images were not downloaded more then 80%") Then
            intIsCompiled = 2
            intUpload = 2
            intIsYoutube = 2
            intIsMp4VideoUploaded = 2
            intIswebmvideo = 2
            intIsWebmVideoUploaded = 2
            intIssmartphonevideo = 2
            intIsSmartphoneVideoUploaded = 2
            intIsuploadmp4thumb = 2
            intIsuploadsmartphonethumb = 2
        ElseIf strerror.Contains("Video not compiled because Overlay Image not downloaded") Then
            intIsCompiled = 2
            intUpload = 2
            intIsYoutube = 2
            intIsMp4VideoUploaded = 2
            intIswebmvideo = 2
            intIsWebmVideoUploaded = 2
            intIssmartphonevideo = 2
            intIsSmartphoneVideoUploaded = 2
            intIsuploadmp4thumb = 2
            intIsuploadsmartphonethumb = 2
        Else
            intIsCompiled = -1
            intUpload = -1
            intIsYoutube = -1
            intIsMp4VideoUploaded = -1
            intIswebmvideo = -1
            intIsWebmVideoUploaded = -1
            intIssmartphonevideo = -1
            intIsSmartphoneVideoUploaded = -1
            intIsuploadmp4thumb = -1
            intIsuploadsmartphonethumb = -1
        End If

        Dim comUpdateData As New MySqlCommand("update videos set videoerror=?videoerror, iscompiled=?iscompiled, isuploaded=?isuploaded, isyoutubeuploaded=?isyoutubeuploaded, ismp4videouploaded=?ismp4videouploaded, iswebmvideo=?iswebmvideo, iswebmvideouploaded=?iswebmvideouploaded, issmartphonevideo=?issmartphonevideo, issmartphonevideouploaded=?issmartphonevideouploaded, isuploadmp4thumb=?isuploadmp4thumb, isuploadsmartphonethumb=?isuploadsmartphonethumb where videoid=?videoid", conVideoError)
        With comUpdateData
            With .Parameters
                Call .AddWithValue("videoerror", strerror)
                Call .AddWithValue("iscompiled", intIsCompiled)
                Call .AddWithValue("isuploaded", intUpload)
                Call .AddWithValue("isyoutubeuploaded", intIsYoutube)
                Call .AddWithValue("ismp4videouploaded", intIsMp4VideoUploaded)
                Call .AddWithValue("iswebmvideo", intIswebmvideo)
                Call .AddWithValue("iswebmvideouploaded", intIsWebmVideoUploaded)
                Call .AddWithValue("issmartphonevideo", intIssmartphonevideo)
                Call .AddWithValue("issmartphonevideouploaded", intIsSmartphoneVideoUploaded)
                Call .AddWithValue("isuploadmp4thumb", intIsuploadmp4thumb)
                Call .AddWithValue("isuploadsmartphonethumb", intIsuploadsmartphonethumb)
                Call .AddWithValue("videoid", videoid)
            End With
            Call .ExecuteNonQuery()
        End With
        conVideoError.Close() : conVideoError.Dispose()
    End Sub

    Private Sub video_Error(ByVal Exception As Animage.AnimageException, ByRef Retry As Boolean) Handles video.Error
        If Exception.Status = Animage.Animage.AnimageVideoStatus.Audio Then
            If blnAudio Then
                Retry = blnAudio
                WriteToAppLog("VideoID : " & Me.intVideoID & " - " & Exception.Status.ToString & "Video_Error in CompileVideo - Retry to fix error - : " & Exception.ToString)
            End If
            blnAudio = False
        ElseIf Exception.Status = Animage.Animage.AnimageVideoStatus.Compress Then
            If blnCompress Then
                Retry = blnCompress
                WriteToAppLog("VideoID : " & Me.intVideoID & " - " & Exception.Status.ToString & "Video_Error in CompileVideo - Retry to fix error - : " & Exception.ToString)
            End If
            blnCompress = False
        ElseIf Exception.Status = Animage.Animage.AnimageVideoStatus.Frames Then
            If blnFrames Then
                Retry = blnFrames
                WriteToAppLog("VideoID : " & Me.intVideoID & " - " & Exception.Status.ToString & "Video_Error in CompileVideo - Retry to fix error - : " & Exception.ToString)
            End If
            blnFrames = False
        ElseIf Exception.Status = Animage.Animage.AnimageVideoStatus.Stream Then
            If blnStream Then
                Retry = blnStream
                WriteToAppLog("VideoID : " & Me.intVideoID & " - " & Exception.Status.ToString & "Video_Error in CompileVideo - Retry to fix error - : " & Exception.ToString)
            End If
            blnStream = False
        End If
        WriteToErrorLog("VideoID : " & Me.intVideoID & " - " & Exception.Status.ToString & " : " & Exception.ToString)
        WriteToAppLog("VideoID : " & Me.intVideoID & " - " & Exception.Status.ToString & " : " & Exception.ToString)
        UpdateVideoError(Exception.ToString, Me.intVideoID)
    End Sub

    Private Sub audio_Error(ByVal Exception As System.Exception) Handles audio.Error
        WriteToErrorLog("EasyAudio Exception : " & Exception.ToString)
    End Sub

    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
