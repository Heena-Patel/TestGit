Imports System.Threading
Imports System.Net
Imports System.IO
Imports System.Xml
Imports MySql.Data.MySqlClient
Imports System.Net.Mail

Public Class Main


#Region "Variablea and Constants"

    Private thrdMain As Thread
    Private thrdUpload As Thread

    Dim CompileMaxThread As Integer = Common.ReadSetting("CompileMaxThread")
    Dim UploadMaxThread As Integer = Common.ReadSetting("UploadMaxThread")



    Private WithEvents qCompile As New Queue(CompileMaxThread)
    Private WithEvents qUpload As New Queue(UploadMaxThread)

    Private IsCompileQueueRunning As Boolean = False
    Private IsUploadQueueRunning As Boolean = False

    Private arrUpload As New ArrayList
    Dim arrProcessingList As New ArrayList

#End Region


#Region "Procedures and Functions"

    Public Sub StartThread()
        Try
            logApp.OpenLog("Error.log")
            logApp.OpenLogFile()
            WriteToAppLog("Service started Successfully.")

            If Not Database.OpenConnection Then
                WriteToAppLog("Connection not started")
                Exit Sub
            End If


            NeoSpeechEmail = Common.ReadSetting("NeoSpeechEmail")
            NeoSpeechAccountId = Common.ReadSetting("NeoSpeechAccountId")
            NeoSpeechLoginKey = Common.ReadSetting("NeoSpeechLoginKey")
            NeoSpeechLoginPwd = Common.ReadSetting("NeoSpeechLoginPwd")

            '''' My Machine Key 
          
            Animage.License.Key = "DA5F-4B54-30DD-28F2"
            EasyAudio.License.Key = "DA5F-4B54-30DD-28F2"

            '' PropertyTube_Server EC2 Machine Key
            'Animage.License.Key = "7168-3010-3271-7EFD"
            'EasyAudio.License.Key = "7168-3010-3271-7EFD"

            '' PropertyTube_Server4 Machine Key for 51
            'Animage.License.Key = "FE3D-02FA-C9AD-F7B7"
            'EasyAudio.License.Key = "FE3D-02FA-C9AD-F7B7"

            '' PropertyTube_Server5 Machine Key for 99
            'Animage.License.Key = "C3AB-D9B1-32FD-A39B"
            'EasyAudio.License.Key = "C3AB-D9B1-32FD-A39B"

            ' PropertyTube Server 6 Machine Key for 35
            'Animage.License.Key = "C3AB-D9B1-32FD-A39B"
            'EasyAudio.License.Key = "C3AB-D9B1-32FD-A39B"

            Call FTPSetting.RefreshFTPSettings()

            Call StartMainThread()
            '    Call StartUploadingThread()

            WriteToAppLog("Service Started Successfully")

        Catch ex As Exception
            WriteToErrorLog("StartThread Procedures : " & ex.ToString)
        End Try
    End Sub

    Public Sub StopThread()
        Try
            Try
                If Not thrdMain Is Nothing Then
                    If thrdMain.IsAlive Then
                        thrdMain.Abort()
                        WriteToAppLog("Compile Thread Aborted")
                    End If
                End If
            Catch ex As Exception
                WriteToAppLog("Compile Thread aborted Error : " & ex.ToString)
                WriteToErrorLog("Compile Thread aborted Error : " & ex.ToString)
            End Try
            Try
                If Not thrdUpload Is Nothing Then
                    If thrdUpload.IsAlive Then
                        thrdUpload.Abort()
                        WriteToAppLog("Upload Thread Aborted")
                    End If
                End If
            Catch ex As Exception
                WriteToErrorLog("Upload Thread aborted Error : " & ex.ToString)
                WriteToAppLog("Upload Thread aborted Error : " & ex.ToString)
            End Try
            Database.CloseConnection()
            WriteToAppLog("Service Stopped Successfully")
        Catch ex As Exception
            WriteToErrorLog("StopThread Procedures : " & ex.ToString)
        End Try
    End Sub

    Private Sub StartMainThread()
        Try
            thrdMain = New Thread(AddressOf RefreshMainThread)
            Call thrdMain.Start()
        Catch ex As Exception
            WriteToErrorLog("StartMainThread Procedure : " & ex.ToString)
        End Try
    End Sub

    Private Sub RefreshMainThread()
        Dim conRefresh As MySqlConnection = Nothing
        Try
            Do While True
                Try
                    If IsCompileQueueRunning = False Then

                        If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
                        conRefresh = Database.Connection.Clone
                        If Not conRefresh.State = ConnectionState.Open Then Call conRefresh.Open()

                        Dim intRemainVideos As Integer = Database.ExecuteSQL("select count(*) as cntRemainVideos from videos" & _
                                                                            " where (iscompiled = 0 OR isuploaded=0 OR isyoutubeuploaded = 0 OR ismp4videouploaded = 0 or iswebmvideo = 0 or iswebmvideouploaded = 0 or issmartphonevideouploaded = 0 or isuploadmp4thumb = 0 or isuploadsmartphonethumb = 0 or isyoutubeonly=0) and feedid=(select max(feedid) from feeds)", Nothing, conRefresh)
                        WriteToAppLog("UpdateXMLData Remain Videos counts : " & intRemainVideos)
                        If intRemainVideos = 0 Then
                            Call UpdateXMLData()
                        End If
                        Call RefreshCompilationQueue()
                    End If
                    If Not conRefresh Is Nothing Then conRefresh.Close() : conRefresh.Dispose()
                Catch ex As Exception
                    WriteToErrorLog("RefreshMainThread Inner Procedure : " & ex.ToString)
                End Try
                Thread.Sleep(New TimeSpan(0, 5, 0))
            Loop

        Catch ex As Exception
            WriteToErrorLog("RefreshMainThread Procedure : " & ex.ToString)
        End Try
    End Sub

    Private Sub UpdateXMLData()
        Dim conXmlData As MySqlConnection = Nothing
        If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
        conXmlData = Database.Connection.Clone
        If Not conXmlData.State = ConnectionState.Open Then Call conXmlData.Open()
        Dim FileName As String = ""
        Try
            'Dim Licenserequest As HttpWebRequest = HttpWebRequest.Create("http://www.ourclientwebsites.com/propertytubelicense.php?licensekey=1313-7D0B-7E05-7784")
            'Licenserequest.Method = "PUT"
            'Dim LicenseOutput As New StreamReader(Licenserequest.GetResponse.GetResponseStream, True)
            'If LicenseOutput.ReadToEnd = 1 Then


            ' '' '' '' ''Dim request As System.Net.HttpWebRequest = System.Net.HttpWebRequest.Create(FTPSetting.XMLFilePath)
            ' '' '' '' ''request.Method = "GET"

            ' '' '' '' ''If FTPSetting.IsCredentials Then
            ' '' '' '' ''    request.Credentials = New NetworkCredential(FTPSetting.FileUsername, FTPSetting.FilePassword)
            ' '' '' '' ''End If

            ' '' '' '' ''Dim response As HttpWebResponse = DirectCast(request.GetResponse, HttpWebResponse)
            ' '' '' '' ''Dim ResponseStream As Stream = response.GetResponseStream

            ' '' '' '' ''If Not Directory.Exists(Application.StartupPath & "\XMLFeed") Then
            ' '' '' '' ''    Directory.CreateDirectory(Application.StartupPath & "\XMLFeed")
            ' '' '' '' ''End If

            Dim CurrentDate As DateTime = Now
            ' '' '' '' ''FileName = Application.StartupPath & "\XMLFeed\PropertyTubeXML_" & Format(CurrentDate, "MMddyyhhmmss").ToString & ".xml"
            ' '' '' '' ''Dim Stream As FileStream = New FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)

            ' '' '' '' ''Dim data(4096) As Byte
            ' '' '' '' ''Dim intOffset As Int64 = 0
            ' '' '' '' ''Dim intReadBytes As Integer
            '' '' '' '' ''Dim TotalBytes As Byte = ResponseStream.Length
            ' '' '' '' ''Do
            ' '' '' '' ''    intReadBytes = ResponseStream.Read(data, 0, data.Length)
            ' '' '' '' ''    intOffset += intReadBytes
            ' '' '' '' ''    Stream.Write(data, 0, intReadBytes)

            ' '' '' '' ''Loop While intReadBytes > 0

            ' '' '' '' ''Stream.Close()
            ' '' '' '' ''Stream.Dispose()
            ' '' '' '' ''ResponseStream.Close()
            ' '' '' '' ''ResponseStream.Dispose()

            FileName = AppDomain.CurrentDomain.BaseDirectory & "\XMLFeed\PropertyTubeXML_101216120103.xml"
            Dim XMLLocalDoc As New XmlDocument
            XMLLocalDoc.Load(FileName)
            Dim RootNodeList As XmlNodeList = XMLLocalDoc.SelectNodes("PtSlides/PtSlideshowConversion")
            Dim RootNode As XmlNode
            If RootNodeList.Count > 0 Then

                'Insert Feed details
                Dim intFeedID As Integer
                Dim FeedFileName As String = Path.GetFileName(FileName)
                Dim FeedDate As String = Format(CurrentDate, "yyyy-MM-dd HH:mm:ss")

                Dim cmdFeeds As New MySqlCommand("insert into feeds(feedfilename, feeddate, totalvideos, creationdate) values(?feedfilename, ?feeddate, ?totalvideos, ?creationdate);SELECT LAST_INSERT_ID()", conXmlData)
                With cmdFeeds
                    With .Parameters
                        Call .AddWithValue("feedfilename", FeedFileName)
                        Call .AddWithValue("feeddate", FeedDate)
                        Call .AddWithValue("totalvideos", RootNodeList.Count)
                        Call .AddWithValue("creationdate", Format(Now, "yyyy-MM-dd HH:mm:ss"))
                    End With
                    intFeedID = .ExecuteScalar
                End With

                For Each RootNode In RootNodeList
                    Try
                        Dim ClientChildNodeList As XmlNodeList = RootNode.SelectNodes("./client/*")
                        Dim IsYoutubeCredentials As Boolean = False
                        Dim UserID As String = ""
                        Dim UserName As String = ""
                        Dim Email As String = ""
                        Dim YoutubeUsername As String = ""
                        Dim YoutubePassword As String = ""
                        Dim YoutubeAPIKey As String = ""
                        Dim intUserID As New Integer
                        Dim Country As String = ""

                        For Each ClientChildNode As XmlNode In ClientChildNodeList
                            If ClientChildNode.Name = "userId" Then
                                UserID = ClientChildNode.InnerText
                            ElseIf ClientChildNode.Name = "name" Then
                                UserName = ClientChildNode.InnerText
                            ElseIf ClientChildNode.Name = "email" Then
                                Email = ClientChildNode.InnerText
                            ElseIf ClientChildNode.Name = "country" Then
                                Country = ClientChildNode.InnerText
                            ElseIf ClientChildNode.Name = "youtubeUsername" Then
                                YoutubeUsername = ClientChildNode.InnerText
                            ElseIf ClientChildNode.Name = "youtubePassword" Then
                                YoutubePassword = ClientChildNode.InnerText
                            ElseIf ClientChildNode.Name = "youtubeApikey" Then
                                YoutubeAPIKey = ClientChildNode.InnerText
                            End If
                        Next

                        If Not Common.ConvertNull(YoutubeUsername) = "" And Not Common.ConvertNull(YoutubePassword) = "" And Not Common.ConvertNull(YoutubeAPIKey) = "" Then
                            IsYoutubeCredentials = True
                        Else
                            IsYoutubeCredentials = False
                        End If

                        Dim intCount As Integer = Database.ExecuteSQL("select count(*) from users where userid=" & UserID, Nothing, conXmlData)
                        If Not intCount > 0 Then
                            Database.ExecuteSQL("insert into users(userid, username, email, country, youtubeusername, youtubepassword, youtubeapikey) values(" & UserID & ", '" & UserName & "', '" & Email & "', '" & Country & "', '" & YoutubeUsername & "', '" & YoutubePassword & "', '" & YoutubeAPIKey & "')", Nothing, conXmlData)
                        Else
                            Database.ExecuteSQL("update users set username='" & UserName & "', email='" & Email & "', country='" & Country & "', youtubeusername='" & YoutubeUsername & "', youtubepassword='" & YoutubePassword & "', youtubeapikey='" & YoutubeAPIKey & "' where userid=" & UserID, Nothing, conXmlData)
                        End If
                        intUserID = UserID

                        Dim BucketName As String = ""
                        Dim FolderName As String = ""
                        Dim IsYoutubeUploadOnly As Boolean = False
                        Dim IsYoutubeRemovalOnly As Boolean = False

                        Dim DestinationChildNodeList As XmlNodeList = RootNode.SelectNodes("./destination/*")
                        For Each DestinationChildNode As XmlNode In DestinationChildNodeList
                            If DestinationChildNode.Name = "bucket" Then
                                BucketName = DestinationChildNode.InnerText
                            ElseIf DestinationChildNode.Name = "folder" Then
                                FolderName = DestinationChildNode.InnerText
                            ElseIf DestinationChildNode.Name = "youtubeUploadOnly" Then
                                If DestinationChildNode.InnerText = "true" Then
                                    IsYoutubeUploadOnly = True
                                Else
                                    IsYoutubeUploadOnly = False
                                End If
                            ElseIf DestinationChildNode.Name = "youtubeRemovalOnly" Then
                                If DestinationChildNode.InnerText = "true" Then
                                    IsYoutubeRemovalOnly = True
                                Else
                                    IsYoutubeRemovalOnly = False
                                End If
                            End If
                        Next

                        Dim OverlayedImagePath As String = ""
                        Dim PropertyID As Integer
                        Dim AmazoneFilePath As String = ""
                        Dim VideoFileName As String = ""
                        Dim mp4ThumbFilename As String = ""
                        Dim WebmVideoName As String = ""
                        Dim SmartPhoneVideoName As String = ""
                        Dim SmartPhoneThumbFileName As String = ""
                        Dim VoiceOver As Integer
                        Dim Theme As Integer = 0
                        Dim intStoreOnFTP As Integer = 0

                        Dim VideoChildNodeList As XmlNodeList = RootNode.SelectNodes("./video/*")
                        For Each VideoChildNode As XmlNode In VideoChildNodeList
                            If VideoChildNode.Name = "overlayImage" Then
                                OverlayedImagePath = VideoChildNode.InnerText
                            ElseIf VideoChildNode.Name = "voiceOver" Then
                                VoiceOver = CInt(VideoChildNode.InnerText)
                            ElseIf VideoChildNode.Name = "theme" Then
                                Theme = CInt(VideoChildNode.InnerText)
                            ElseIf VideoChildNode.Name = "propertyId" Then
                                PropertyID = VideoChildNode.InnerText
                            ElseIf VideoChildNode.Name = "formats" Then
                                VideoFileName = VideoChildNode.SelectSingleNode("format[@type='sd-mp4']/filename").InnerText
                                mp4ThumbFilename = VideoChildNode.SelectSingleNode("format[@type='sd-mp4']/thumbnail").InnerText
                                WebmVideoName = VideoChildNode.SelectSingleNode("format[@type='sd-webm']/filename").InnerText
                                SmartPhoneVideoName = VideoChildNode.SelectSingleNode("format[@type='smartphone-mp4']/filename").InnerText
                                SmartPhoneThumbFileName = VideoChildNode.SelectSingleNode("format[@type='smartphone-mp4']/thumbnail").InnerText

                                VideoFileName = VideoFileName.Substring(0, VideoFileName.Length - 4)
                                mp4ThumbFilename = mp4ThumbFilename.Substring(0, mp4ThumbFilename.Length - 4)
                                WebmVideoName = WebmVideoName.Substring(0, WebmVideoName.Length - 5)
                                SmartPhoneVideoName = SmartPhoneVideoName.Substring(0, SmartPhoneVideoName.Length - 4)
                                SmartPhoneThumbFileName = SmartPhoneThumbFileName.Substring(0, SmartPhoneThumbFileName.Length - 4)

                            ElseIf VideoChildNode.Name = "sourceFilename" Then
                                AmazoneFilePath = VideoChildNode.InnerText
                                If Not AmazoneFilePath = "" Then
                                    Dim AmazoneInfo() As String = AmazoneFilePath.Split("/")
                                    If Not AmazoneInfo Is Nothing Then
                                        FolderName = AmazoneInfo(3)
                                        VideoFileName = AmazoneInfo(4)
                                        Dim BucketInfo() As String = AmazoneInfo(2).Split(".")
                                        If Not BucketInfo Is Nothing Then
                                            BucketName = BucketInfo(0)
                                        End If
                                    End If
                                End If
                                VideoFileName = VideoFileName.Substring(0, VideoFileName.Length - 4)
                            ElseIf VideoChildNode.Name = "storeCopyOnFtp" Then
                                intStoreOnFTP = CInt(VideoChildNode.InnerText)
                            End If
                        Next

                        Dim YoutubeTitle As String = ""
                        Dim Description As String = ""
                        Dim KeyWords As String = ""
                        Dim index As Integer = 1
                        Dim PropertyPrice As String = ""
                        Dim Bedrooms As String = ""
                        Dim Bathrooms As String = ""
                        Dim Location As String = ""
                        Dim FullDescription As String = ""
                        Dim SpokenTitle As String = ""
                        Dim intCommercial As Integer = 0
                        Dim intSale As Integer = 0
                        Dim intRent As Integer = 0

                        Dim PropertyDetailsChildNodeList As XmlNodeList = RootNode.SelectNodes("./propertyDetails/*")
                        For Each PropertyDetailsChildNode As XmlNode In PropertyDetailsChildNodeList

                            If PropertyDetailsChildNode.Name = "title" Then
                                YoutubeTitle = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "spokenTitle" Then
                                SpokenTitle = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "youtubeDescription" Then
                                Description = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "tags" Then

                                Dim TagNodeList As XmlNodeList = PropertyDetailsChildNode.SelectNodes("./*") ' XPath Query used for getting child nodes of Tags
                                For Each TagNode As XmlNode In TagNodeList
                                    If index = 1 Then
                                        KeyWords = TagNode.InnerText
                                    Else
                                        KeyWords += "," & TagNode.InnerText
                                    End If
                                    index += 1
                                Next

                            ElseIf PropertyDetailsChildNode.Name = "propertyPrice" Then
                                PropertyPrice = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "bedrooms" Then
                                Bedrooms = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "bathrooms" Then
                                Bathrooms = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "location" Then
                                Location = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "fullDescription" Then
                                FullDescription = PropertyDetailsChildNode.InnerText
                            ElseIf PropertyDetailsChildNode.Name = "propertyType" Then
                                If PropertyDetailsChildNode.InnerText = "Commercial" Then
                                    intCommercial = 1
                                Else
                                    intCommercial = 0
                                End If
                            ElseIf PropertyDetailsChildNode.Name = "sale" Then
                                If PropertyDetailsChildNode.InnerText = "1" Then
                                    intSale = 1
                                Else
                                    intSale = 0
                                End If
                            ElseIf PropertyDetailsChildNode.Name = "rent" Then
                                If PropertyDetailsChildNode.InnerText = "1" Then
                                    intRent = 1
                                Else
                                    intRent = 0
                                End If
                            End If
                        Next

                        Dim AgentID As Integer = 0
                        Dim AgentName As String = ""
                        Dim AgentEmail As String = ""
                        Dim Mobile As String = ""
                        Dim OfficeNo As String = ""
                        Dim AgentImage As String = ""
                        Dim intIsAgentExist As Integer = 0

                        Dim PropertyAgentsChildNodeList As XmlNodeList = RootNode.SelectNodes("./propertyAgents/*")
                        If PropertyAgentsChildNodeList.Count > 0 Then
                            For Each PropertyAgentsChildNode As XmlNode In PropertyAgentsChildNodeList
                                If PropertyAgentsChildNode.Name = "propertyAgent" Then
                                    For Each Attribute As XmlAttribute In PropertyAgentsChildNode.Attributes
                                        If Attribute.Name = "id" Then
                                            AgentID = Attribute.Value
                                        ElseIf Attribute.Name = "name" Then
                                            AgentName = Attribute.Value
                                        ElseIf Attribute.Name = "email" Then
                                            AgentEmail = Attribute.Value
                                        ElseIf Attribute.Name = "mobile" Then
                                            Mobile = Attribute.Value
                                        ElseIf Attribute.Name = "officeNumber" Then
                                            OfficeNo = Attribute.Value
                                        ElseIf Attribute.Name = "image" Then
                                            AgentImage = Attribute.Value
                                        End If
                                    Next

                                End If
                            Next
                            intIsAgentExist = 1
                        Else
                            intIsAgentExist = 0
                        End If


                        Dim IsDownloadedOverlayImage As Integer = 0
                        Dim intVideoID As Integer
                        Dim IsCompiled As Integer
                        Dim IsUploaded As Integer = 0
                        Dim IsYoutubeUploaded As Integer
                        Dim IsMp4VideoUploaded As Integer
                        Dim IsWebmVideo As Integer
                        Dim IsWebmVideoUploaded As Integer
                        Dim IsSmartphoneVideo As Integer
                        Dim IsSmartphoneVideoUploaded As Integer
                        Dim IsUploadMp4Thumb As Integer
                        Dim IsUploadSmartphoneThumb As Integer
                        Dim IsYoutubeOnly As Integer
                        Dim IsQueue As Boolean = False
                        If IsYoutubeUploadOnly Then
                            IsCompiled = 1
                            IsUploaded = 1
                            IsQueue = True
                            If IsYoutubeCredentials Then
                                IsYoutubeUploaded = 0
                            Else
                                IsYoutubeUploaded = 1
                            End If
                            IsDownloadedOverlayImage = -1
                            IsMp4VideoUploaded = 1
                            IsWebmVideo = 1
                            IsWebmVideoUploaded = 1
                            IsSmartphoneVideo = 1
                            IsSmartphoneVideoUploaded = 1
                            IsUploadMp4Thumb = 1
                            IsUploadSmartphoneThumb = 1
                            IsYoutubeOnly = 0
                        ElseIf IsYoutubeRemovalOnly Then
                            IsCompiled = 1
                            IsUploaded = 1
                            IsQueue = True
                            IsYoutubeUploaded = 1
                            IsDownloadedOverlayImage = -1
                            IsMp4VideoUploaded = 1
                            IsWebmVideo = 1
                            IsWebmVideoUploaded = 1
                            IsSmartphoneVideo = 1
                            IsSmartphoneVideoUploaded = 1
                            IsUploadMp4Thumb = 1
                            IsUploadSmartphoneThumb = 1
                            IsYoutubeOnly = 0

                        Else
                            IsCompiled = 0
                            If intStoreOnFTP = 0 Then
                                IsUploaded = 2
                            Else
                                IsUploaded = 0
                            End If
                            IsQueue = False
                            If IsYoutubeCredentials Then
                                IsYoutubeUploaded = 0
                            Else
                                IsYoutubeUploaded = 1
                            End If

                            If OverlayedImagePath.Equals("") Then
                                IsDownloadedOverlayImage = -1
                            Else
                                IsDownloadedOverlayImage = 0
                            End If

                            IsMp4VideoUploaded = 0
                            IsWebmVideo = 0
                            IsWebmVideoUploaded = 0
                            IsSmartphoneVideo = 0
                            IsSmartphoneVideoUploaded = 0
                            IsUploadMp4Thumb = 0
                            IsUploadSmartphoneThumb = 0
                            IsYoutubeOnly = -1
                        End If

                        'Insert Videos Details in video table
                        Dim cmdVideos As New MySqlCommand("insert into videos(videoname, creationdate, iscompiled, isuploaded, isqueued, userid, isyoutubeuploaded, theme, iscommercial, voiceover, isstoreonftp, propertyid, sdwebmvideoname, smartphonevideoname, mp4thumbnail, smartphonethumbnail, bucket, foldername, ismp4videouploaded, iswebmvideo, iswebmvideouploaded, issmartphonevideo, issmartphonevideouploaded, isuploadmp4thumb, isuploadsmartphonethumb, amazonefilepath, spokentitle, youtubetitle, youtubekeywords, youtubedescription, youtubeuploadonly, youtuberemovalonly, isyoutubeonly, feedid, overlayimagepath, isdownloadedoverlayimage) values(?videoname, ?creationdate, ?iscompiled, ?isuploaded, ?isqueued, ?userid, ?isyoutubeuploaded, ?theme, ?iscommercial, ?voiceover, ?isstoreonftp, ?propertyid, ?sdwebmvideoname, ?smartphonevideoname, ?mp4thumbnail, ?smartphonethumbnail, ?bucket, ?foldername, ?ismp4videouploaded, ?iswebmvideo, ?iswebmvideouploaded, ?issmartphonevideo, ?issmartphonevideouploaded, ?isuploadmp4thumb, ?isuploadsmartphonethumb, ?amazonefilepath, ?spokentitle, ?youtubetitle, ?youtubekeywords, ?youtubedescription, ?youtubeuploadonly, ?youtuberemovalonly, ?isyoutubeonly, ?feedid, ?overlayimagepath, ?isdownloadedoverlayimage);SELECT LAST_INSERT_ID()", conXmlData)
                        With cmdVideos
                            With .Parameters
                                Call .AddWithValue("videoname", VideoFileName)
                                Call .AddWithValue("creationdate", Format(Now, "yyyy-MM-dd HH:mm:ss"))
                                Call .AddWithValue("iscompiled", IsCompiled)
                                Call .AddWithValue("isuploaded", IsUploaded)
                                Call .AddWithValue("isqueued", IsQueue)
                                Call .AddWithValue("userid", intUserID)
                                Call .AddWithValue("isyoutubeuploaded", IsYoutubeUploaded)
                                Call .AddWithValue("theme", Theme)
                                Call .AddWithValue("iscommercial", intCommercial)
                                Call .AddWithValue("voiceover", VoiceOver)
                                Call .AddWithValue("isstoreonftp", intStoreOnFTP)
                                Call .AddWithValue("propertyid", PropertyID)
                                Call .AddWithValue("sdwebmvideoname", WebmVideoName)
                                Call .AddWithValue("smartphonevideoname", SmartPhoneVideoName)
                                Call .AddWithValue("mp4thumbnail", mp4ThumbFilename)
                                Call .AddWithValue("smartphonethumbnail", SmartPhoneThumbFileName)
                                Call .AddWithValue("bucket", BucketName)
                                Call .AddWithValue("foldername", FolderName)
                                Call .AddWithValue("ismp4videouploaded", IsMp4VideoUploaded)
                                Call .AddWithValue("iswebmvideo", IsWebmVideo)
                                Call .AddWithValue("iswebmvideouploaded", IsWebmVideoUploaded)
                                Call .AddWithValue("issmartphonevideo", IsSmartphoneVideo)
                                Call .AddWithValue("issmartphonevideouploaded", IsSmartphoneVideoUploaded)
                                Call .AddWithValue("isuploadmp4thumb", IsUploadMp4Thumb)
                                Call .AddWithValue("isuploadsmartphonethumb", IsUploadSmartphoneThumb)
                                Call .AddWithValue("spokentitle", SpokenTitle)
                                Call .AddWithValue("youtubetitle", YoutubeTitle)
                                Call .AddWithValue("youtubekeywords", KeyWords)
                                Call .AddWithValue("youtubedescription", Description)
                                Call .AddWithValue("amazonefilepath", AmazoneFilePath)
                                Call .AddWithValue("youtubeuploadonly", IsYoutubeUploadOnly)
                                Call .AddWithValue("youtuberemovalonly", IsYoutubeRemovalOnly)
                                Call .AddWithValue("isyoutubeonly", IsYoutubeOnly)
                                Call .AddWithValue("feedid", intFeedID)
                                Call .AddWithValue("overlayimagepath", OverlayedImagePath)
                                Call .AddWithValue("isdownloadedoverlayimage", IsDownloadedOverlayImage)
                            End With
                            intVideoID = .ExecuteScalar()
                        End With

                        'Insert images details in images table
                        Dim IsDownloaded As Boolean = False
                        Dim SourceMediaChildNodeList As XmlNodeList = RootNode.SelectNodes("./sourceMedia/*")
                        For Each SourceMediaChildNode As XmlNode In SourceMediaChildNodeList
                            Dim ImagePath As String = SourceMediaChildNode.ChildNodes(0).InnerText 'URL Tag innerTax
                            ' Database.ExecuteSQL("insert into images(videoid, imagepath, isdownloaded, creationdate) values(" & intVideoID & ", '" & ImagePath & "',  " & IsDownloaded & " , '" & Format(Now, "yyyy-MM-dd HH:mm:ss") & "')", Nothing, conXmlData)
                            Try
                                Dim cmdImagesDetail As New MySqlCommand("insert into images(videoid, imagepath, isdownloaded, creationdate) values(?videoid, ?imagepath, ?isdownloaded, ?creationdate)", conXmlData)
                                With cmdImagesDetail
                                    With .Parameters
                                        Call .AddWithValue("videoid", intVideoID)
                                        Call .AddWithValue("imagepath", ImagePath)
                                        Call .AddWithValue("isdownloaded", IsDownloaded)
                                        Call .AddWithValue("creationdate", Format(Now, "yyyy-MM-dd HH:mm:ss"))
                                    End With
                                    .ExecuteNonQuery()
                                End With
                            Catch ex As Exception
                                WriteToAppLog("XML data Image inserted Failed. " & ex.Message)
                                WriteToErrorLog("UpdateXMLData Procedure Image inserted Failed : " & ex.ToString)
                            End Try
                            IsQueue = True
                            Database.ExecuteSQL("update videos set isqueued=" & IsQueue & " where videoid=" & intVideoID, Nothing, conXmlData)
                        Next

                        'Insert Property details in propertydetails table
                        Dim IsMapDownloaded As Integer = 0
                        If Location.Equals("") Then
                            IsMapDownloaded = -1
                        Else
                            IsMapDownloaded = 0
                        End If

                        Dim cmdPropertyDetails As New MySqlCommand("insert into propertydetails(videoid, price, bedrooms, bathrooms, location, fulldescription, ismapdownloaded, forsale, forrent) values(?videoid, ?price, ?bedrooms, ?bathrooms, ?location, ?fulldescription, ?ismapdownloaded, ?forsale, ?forrent)", conXmlData)
                        With cmdPropertyDetails
                            With .Parameters
                                Call .AddWithValue("videoid", intVideoID)
                                Call .AddWithValue("price", PropertyPrice)
                                Call .AddWithValue("bedrooms", Bedrooms)
                                Call .AddWithValue("bathrooms", Bathrooms)
                                Call .AddWithValue("location", Location)
                                Call .AddWithValue("fulldescription", FullDescription)
                                Call .AddWithValue("ismapdownloaded", IsMapDownloaded)
                                Call .AddWithValue("forsale", intSale)
                                Call .AddWithValue("forrent", intRent)
                            End With
                            .ExecuteNonQuery()
                        End With

                        'Insert agents details in propertyagents table
                        Dim IsPhotoDownloaded As Integer = 0
                        If AgentImage.Equals("") Then
                            IsPhotoDownloaded = -1
                        Else
                            IsPhotoDownloaded = 0
                        End If

                        Dim cmdPropertyAgents As New MySqlCommand("insert into propertyagents(videoid, agentid, name, email, mobile, officenumber, agentimage, isphotodownloaded, isexistagent) values(?videoid, ?agentid, ?name, ?email, ?mobile, ?officenumber, ?agentimage, ?isphotodownloaded, ?isexistagent)", conXmlData)
                        With cmdPropertyAgents
                            With .Parameters
                                Call .AddWithValue("videoid", intVideoID)
                                Call .AddWithValue("agentid", AgentID)
                                Call .AddWithValue("name", AgentName)
                                Call .AddWithValue("email", AgentEmail)
                                Call .AddWithValue("mobile", Mobile)
                                Call .AddWithValue("officenumber", OfficeNo)
                                Call .AddWithValue("agentimage", AgentImage)
                                Call .AddWithValue("isphotodownloaded", IsPhotoDownloaded)
                                Call .AddWithValue("isexistagent", intIsAgentExist)
                            End With
                            .ExecuteNonQuery()
                        End With
                    Catch ex As Exception
                        WriteToAppLog("XML data inserted Failed. " & ex.Message)
                        WriteToErrorLog("UpdateXMLData Procedure : " & ex.ToString)
                    End Try
                Next
                WriteToAppLog("UpdateXMLData Procedure : XML Data Updated Successfully")
            Else
                WriteToAppLog("UpdateXMLData Procedure : No Videos found in feed")
            End If
            'End If
        Catch ex As Exception
            WriteToAppLog("UpdateXMLData Procedure : Error in XML Data Updation" & ex.ToString)
            WriteToAppLog("UpdateXMLData Procedure : Error XML Path" & FileName)
            WriteToErrorLog("UpdateXMLData Procedures : " & ex.ToString)
            Call SendXMLUpdationFailedEmail(ex.Message)
        End Try
        If conXmlData IsNot Nothing Then Call conXmlData.Close() : Call conXmlData.Dispose()
    End Sub

    Private Sub RefreshCompilationQueue()

        Dim conCompile As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conCompile = Database.Connection.Clone
            If Not conCompile.State = ConnectionState.Open Then Call conCompile.Open()
            If IsCompileQueueRunning = False Then
                Dim dtCompile As New DataTable
                dtCompile = Database.FetchDataTable("SELECT videoid from videos where (iscompiled=0 or iscompiled=-1) and isqueued=1", Nothing, conCompile)
                If dtCompile.Rows.Count > 0 Then
                    For Each drRow As DataRow In dtCompile.Rows
                        Call qCompile.Add(New Compiling(drRow.Item("videoid")))
                        WriteToAppLog("VideoID - " & drRow.Item("videoid") & " is added in Compilation Queue")
                    Next
                End If
            End If
        Catch ex As Exception
            WriteToErrorLog("RefreshCompilationQueue Inner Procedures : " & ex.ToString)
        End Try
        If conCompile IsNot Nothing Then Call conCompile.Close() : Call conCompile.Dispose()
    End Sub


    Private Sub StartUploadingThread()
        Try
            thrdUpload = New Thread(AddressOf RefreshUploadQueue)
            Call thrdUpload.Start()
        Catch ex As Exception
            WriteToErrorLog("StartProcessingThread Procedures : " & ex.ToString)
        End Try
    End Sub

    Private Sub RefreshUploadQueue()
        Dim conRefreshYoutube As MySqlConnection = Nothing
        Try
            Do While True
                Try
                    If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
                    conRefreshYoutube = Database.Connection.Clone
                    If Not conRefreshYoutube.State = ConnectionState.Open Then Call conRefreshYoutube.Open()
                    If IsUploadQueueRunning = False Then
                        Dim dtYoutube As New DataTable
                        'Iscompiled = -1 is used for getting failed compiled videos during upload queue is completed.
                        dtYoutube = Database.FetchDataTable("SELECT videoid from videos where ((iscompiled=1 or iscompiled=-1) and (isuploaded=0 OR isyoutubeuploaded=0 or isyoutubeuploaded=-1 or isyoutubeonly=0 or iswebmvideo=0 or iswebmvideo=-1 or issmartphonevideo=0 or issmartphonevideo=-1 or ismp4videouploaded=0 or ismp4videouploaded=-1 or iswebmvideouploaded=0 or iswebmvideouploaded=-1 or issmartphonevideouploaded=0 or issmartphonevideouploaded=-1 or isuploadmp4thumb=0 or isuploadmp4thumb=-1 or isuploadsmartphonethumb=0 or isuploadsmartphonethumb=-1)) and isqueued=1 Union select Videoid from videos where iscompiled=2 and isprocessing =0", Nothing, conRefreshYoutube)
                        If dtYoutube.Rows.Count > 0 Then
                            arrUpload = New ArrayList
                            For Each drRow As DataRow In dtYoutube.Rows
                                Call qUpload.Add(New Uploading(drRow.Item("videoid")))
                                arrUpload.Add(drRow.Item("videoid"))
                                WriteToAppLog("VideoID - " & drRow.Item("videoid") & " is added in Upload Queue")
                            Next
                        End If
                    End If
                Catch ex As Exception
                    WriteToErrorLog("RefreshUploadQueue Inner Procedures : " & ex.ToString)
                End Try
                If conRefreshYoutube IsNot Nothing Then Call conRefreshYoutube.Close() : Call conRefreshYoutube.Dispose()
                Thread.Sleep(New TimeSpan(0, 5, 0))
            Loop

        Catch ex As Exception
            WriteToAppLog("RefreshUploadQueue Procedure : Error in RefreshInterface" & ex.ToString)
            WriteToErrorLog("RefreshUploadQueue Procedures : " & ex.ToString)
        End Try
        If conRefreshYoutube IsNot Nothing Then Call conRefreshYoutube.Close() : Call conRefreshYoutube.Dispose()

    End Sub

#End Region

#Region "qCompile Events"

    Private Sub qCompile_OnQueueComplete() Handles qCompile.OnQueueComplete
        If Not qCompile.Count > 0 Then
            IsCompileQueueRunning = False
        End If
    End Sub

    Private Sub qCompile_OnQueueError(item As QueueItem, exception As System.Exception) Handles qCompile.OnQueueError

    End Sub

    Private Sub qCompile_OnQueuePause() Handles qCompile.OnQueuePause

    End Sub

    Private Sub qCompile_OnQueueResume() Handles qCompile.OnQueueResume

    End Sub

    Private Sub qCompile_OnQueueStart() Handles qCompile.OnQueueStart
        IsCompileQueueRunning = True
    End Sub

#End Region

#Region "qUpload Events"

    Private Sub qUpload_OnQueueComplete() Handles qUpload.OnQueueComplete
        If Not qUpload.Count > 0 Then
            Call CheckNotification()
            IsUploadQueueRunning = False
        End If
    End Sub

    Private Sub qYoutube_OnQueueError(item As QueueItem, exception As System.Exception) Handles qUpload.OnQueueError

    End Sub

    Private Sub qYoutube_OnQueuePause() Handles qUpload.OnQueuePause

    End Sub

    Private Sub qYoutube_OnQueueResume() Handles qUpload.OnQueueResume

    End Sub

    Private Sub qYoutube_OnQueueStart() Handles qUpload.OnQueueStart
        IsUploadQueueRunning = True
    End Sub

#End Region


    Private Sub CheckNotification()
        Dim conNotification As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conNotification = Database.Connection.Clone
            If Not conNotification.State = ConnectionState.Open Then Call conNotification.Open()
            Dim strVideoID As String = ""
            For i = 0 To arrUpload.Count - 1
                If i = 0 Then
                    strVideoID = arrUpload(i).ToString
                Else
                    strVideoID += "," & arrUpload(i).ToString
                End If
            Next

            Dim drData As DataTableReader = Database.FetchDataReader("select Distinct(feedid) from videos where videoid in(" & strVideoID & ")", Nothing, conNotification)
            Do While drData.Read
                Dim intRemainVideos As Integer = Database.ExecuteSQL("select count(videoid) as cntRemainVideos from videos" & _
                                                                                                                            " where (iscompiled = 0 or isyoutubeuploaded = 0 or ismp4videouploaded = 0 or iswebmvideo = 0 or iswebmvideouploaded = 0 or issmartphonevideouploaded = 0 or isuploadmp4thumb = 0 or isuploadsmartphonethumb = 0) and feedid=" & drData.Item("feedid"), Nothing, conNotification)
                If intRemainVideos = 0 Then
                    WriteToAppLog("FeedID : " & drData.Item("feedid") & " - Send Notification.")

                    Call SendNotificationXML(drData.Item("feedid"))

                    Call SendNotificationEmail(drData.Item("feedid"))

                    If Not arrProcessingList Is Nothing Then
                        If arrProcessingList.Count > 0 Then
                            For i = 0 To arrProcessingList.Count - 1
                                Dim RowData As Object() = arrProcessingList(i)
                                Dim ID As Integer = RowData(0)
                                Dim Status As Integer = RowData(1)

                                Dim cmdCSV As New MySqlCommand("update videos set isprocessing=?isprocessing where videoid=?videoid", conNotification)
                                With cmdCSV
                                    With .Parameters
                                        Call .AddWithValue("isprocessing", Status)
                                        Call .AddWithValue("videoid", ID)
                                    End With
                                    .ExecuteNonQuery()
                                End With
                            Next
                            arrProcessingList.Clear()
                        Else
                            WriteToAppLog("Error - arrProcessingList Count is 0.")
                            WriteToErrorLog("Error - arrProcessingList Count is 0.")
                        End If
                    Else
                        WriteToAppLog("Error - arrProcessingList is nothing.")
                        WriteToErrorLog("Error - arrProcessingList is nothing.")
                    End If
                End If
            Loop
            arrUpload.Clear()
            drData.Close()
        Catch ex As Exception
            WriteToAppLog("CheckNotification Procedure : " & ex.ToString)
            WriteToErrorLog("CheckNotification Procedures : " & ex.ToString)
        End Try
        If conNotification IsNot Nothing Then Call conNotification.Close() : Call conNotification.Dispose()
    End Sub

    Private Sub SendNotificationXML(ByVal FeedID As Integer)
        Dim conNotificationXML As MySqlConnection = Nothing
        Dim sw As StreamWriter = Nothing
        Dim request As FtpWebRequest = Nothing
        arrProcessingList = New ArrayList
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conNotificationXML = Database.Connection.Clone
            If Not conNotificationXML.State = ConnectionState.Open Then Call conNotificationXML.Open()
            If Not Directory.Exists(Application.StartupPath & "/NotificationXML") Then
                Directory.CreateDirectory(Application.StartupPath & "/NotificationXML")
            End If

            Dim strFilename As String = "PropertyTubeNotificationXML_" & Format(Now, "MMddyyhhmmss").ToString & ".xml"
            Dim XMLFileName As String = Application.StartupPath & "\NotificationXML\" & strFilename

            Dim dtIsFeedVideo As DataTable = Database.FetchDataTable("select * from videos where isprocessing=0 and feedid=" & FeedID, Nothing, conNotificationXML)
            If Not dtIsFeedVideo Is Nothing Then
                If dtIsFeedVideo.Rows.Count > 0 Then
                    sw = New StreamWriter(XMLFileName, False)
                    Dim strNotificationXML As String = "<PtSlides>" & vbCrLf
                    Dim index As Integer = 1

                    For Each dr As DataRow In dtIsFeedVideo.Rows
                        Dim intYoutubeStatus As Integer = 0
                        Dim intHDMp4Status As Integer = 0
                        Dim intHDWebmStatus As Integer = 0
                        Dim intSmartphoneStatus As Integer = 0
                        Dim blnIsYoutubeUploadOnly As Boolean = False
                        Dim blnIsYoutubeRemovalOnly As Boolean = False

                        Dim strVideoLocation As String = ""
                        Dim strThumbLocation As String = ""
                        Dim strWebmVideoLocation As String = ""
                        Dim strSmartphoneVideoLocation As String = ""
                        Dim strSmartphoneThumbLocation As String = ""
                        Dim Mp4Filesize As String = ""
                        Dim WebmFileSize As String = ""
                        Dim SmartphoneFileSize As String = ""
                        Dim YoutubeLink As String = ""
                        Dim IsException As Boolean = False
                        Dim IsYoutubeException As Boolean = False

                        If dr.Item("youtubeuploadonly") Then
                            blnIsYoutubeUploadOnly = True
                            If dr.Item("isyoutubeuploaded") = 1 Then
                                YoutubeLink = ConvertNull(dr.Item("youtubelink"), "")
                                YoutubeLink = "<![CDATA[ " & YoutubeLink & "]]>"
                                IsYoutubeException = False
                            Else
                                IsYoutubeException = True
                                intYoutubeStatus = -1
                            End If
                        ElseIf dr.Item("youtuberemovalonly") Then
                            blnIsYoutubeRemovalOnly = True
                            If dr.Item("isyoutubeonly") = 1 Then
                                intYoutubeStatus = 0
                                IsYoutubeException = False
                            Else
                                intYoutubeStatus = -1
                                IsYoutubeException = True
                            End If
                        Else
                            If dr.Item("iscompiled") = 1 Then

                                If dr.Item("ismp4videouploaded") = 1 Then
                                    strVideoLocation = "s3://" & dr.Item("bucket") & ".s3.amazonaws.com/" & dr.Item("foldername")
                                Else
                                    strVideoLocation = ""
                                    intHDMp4Status = -1
                                End If

                                If dr.Item("isuploadmp4thumb") = 1 Then
                                    strThumbLocation = "s3://" & dr.Item("bucket") & ".s3.amazonaws.com/" & dr.Item("foldername")
                                Else
                                    strThumbLocation = ""
                                    intHDMp4Status = -1
                                End If

                                If dr.Item("iswebmvideouploaded") = 1 Then
                                    strWebmVideoLocation = "s3://" & dr.Item("bucket") & ".s3.amazonaws.com/" & dr.Item("foldername")
                                Else
                                    strWebmVideoLocation = ""
                                    intHDWebmStatus = -1
                                End If

                                If dr.Item("issmartphonevideouploaded") = 1 Then
                                    strSmartphoneVideoLocation = "s3://" & dr.Item("bucket") & ".s3.amazonaws.com/" & dr.Item("foldername")
                                Else
                                    strSmartphoneVideoLocation = ""
                                    intSmartphoneStatus = -1
                                End If

                                If dr.Item("isuploadsmartphonethumb") = 1 Then
                                    strSmartphoneThumbLocation = "s3://" & dr.Item("bucket") & ".s3.amazonaws.com/" & dr.Item("foldername")
                                Else
                                    strSmartphoneThumbLocation = ""
                                    intSmartphoneStatus = -1
                                End If

                                Mp4Filesize = ConvertNull(dr.Item("mp4videolength").ToString, "")
                                WebmFileSize = ConvertNull(dr.Item("webmvideolength").ToString, "")
                                SmartphoneFileSize = ConvertNull(dr.Item("smartphonevideolength").ToString, "")

                                blnIsYoutubeUploadOnly = False
                                blnIsYoutubeRemovalOnly = False
                                If dr.Item("isyoutubeuploaded") = 1 Then
                                    YoutubeLink = Common.ConvertNull(dr.Item("youtubelink"), "")
                                    YoutubeLink = "<![CDATA[ " & YoutubeLink & "]]>"
                                    IsYoutubeException = False
                                Else
                                    intYoutubeStatus = -1
                                    IsYoutubeException = True
                                End If

                            Else

                                Dim strError As String = ""
                                strError = Common.ConvertNull(dr.Item("videoerror"), "")
                                If strError.StartsWith("System.Exception: Image Not Found in Directory") Then
                                    IsException = True
                                    intYoutubeStatus = -1
                                    intHDMp4Status = -1
                                    intHDWebmStatus = -1
                                    intSmartphoneStatus = -1
                                    strVideoLocation = ""
                                    strThumbLocation = ""
                                    strWebmVideoLocation = ""
                                    strSmartphoneVideoLocation = ""
                                    strSmartphoneThumbLocation = ""
                                ElseIf strError.StartsWith("System.Exception: Video Compiling Failed because Images were not downloaded more then 80%") Then
                                    IsException = True
                                    intYoutubeStatus = -1
                                    intHDMp4Status = -1
                                    intHDWebmStatus = -1
                                    intSmartphoneStatus = -1
                                    strVideoLocation = ""
                                    strThumbLocation = ""
                                    strWebmVideoLocation = ""
                                    strSmartphoneVideoLocation = ""
                                    strSmartphoneThumbLocation = ""
                                ElseIf strError.Contains("Video not compiled because Overlay Image not downloaded") Then
                                    IsException = True
                                    intYoutubeStatus = -1
                                    intHDMp4Status = -1
                                    intHDWebmStatus = -1
                                    intSmartphoneStatus = -1
                                    strVideoLocation = ""
                                    strThumbLocation = ""
                                    strWebmVideoLocation = ""
                                    strSmartphoneVideoLocation = ""
                                    strSmartphoneThumbLocation = ""
                                Else
                                    IsException = True
                                    'IsException = False    
                                End If

                                intYoutubeStatus = -1
                                intHDMp4Status = -1
                                intHDWebmStatus = -1
                                intSmartphoneStatus = -1

                                blnIsYoutubeUploadOnly = False
                                blnIsYoutubeRemovalOnly = False

                            End If

                        End If

                        strNotificationXML += vbTab & "<PtSlideShow No='" & index.ToString & "'>" & vbCrLf
                        strNotificationXML += vbTab & vbTab & "<Video>" & vbCrLf
                        strNotificationXML += vbTab & vbTab & vbTab & "<PropertyID>" & dr.Item("propertyid") & "</PropertyID>" & vbCrLf

                        If blnIsYoutubeUploadOnly = False And blnIsYoutubeRemovalOnly = False Then

                            strNotificationXML += vbTab & vbTab & vbTab & "<Formats>" & vbCrLf

                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & "<format type='sd-mp4'>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<Status>" & intHDMp4Status.ToString & "</Status>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<filename>" & dr.Item("videoname") & ".mp4" & "</filename>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<thumbnail>" & dr.Item("mp4thumbnail") & ".png" & "</thumbnail>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<FileSize>" & Mp4Filesize & "</FileSize>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<Length>" & Common.ConvertNull(dr.Item("videotime"), "") & "</Length>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<VideoLocation>" & strVideoLocation & "</VideoLocation>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<ThumbnailLocation>" & strThumbLocation & "</ThumbnailLocation>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & "</format>" & vbCrLf

                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & "<format type='sd-webm'>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<Status>" & intHDWebmStatus & "</Status>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<filename>" & dr.Item("sdwebmvideoname") & ".webm" & "</filename>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<FileSize>" & WebmFileSize & "</FileSize>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<Length>" & Common.ConvertNull(dr.Item("videotime"), "") & "</Length>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<VideoLocation>" & strWebmVideoLocation & "</VideoLocation>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & "</format>" & vbCrLf

                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & "<format type='smartphone-mp4'>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<Status>" & intSmartphoneStatus & "</Status>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<filename>" & dr.Item("smartphonevideoname") & ".mp4" & "</filename>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<thumbnail>" & dr.Item("smartphonethumbnail") & ".png" & "</thumbnail>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<FileSize>" & SmartphoneFileSize & "</FileSize>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<Length>" & dr.Item("videotime") & "</Length>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<VideoLocation>" & strSmartphoneVideoLocation & "</VideoLocation>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & vbTab & "<ThumbnailLocation>" & strSmartphoneThumbLocation & "</ThumbnailLocation>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & vbTab & "</format>" & vbCrLf

                            strNotificationXML += vbTab & vbTab & vbTab & "</Formats>" & vbCrLf
                        End If

                        strNotificationXML += vbTab & vbTab & "</Video>" & vbCrLf

                        strNotificationXML += vbTab & vbTab & "<Youtube>" & vbCrLf
                        If blnIsYoutubeUploadOnly Then
                            strNotificationXML += vbTab & vbTab & vbTab & "<IsYoutubeUploadOnly>" & blnIsYoutubeUploadOnly.ToString & "</IsYoutubeUploadOnly>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & "<YoutubeURL>" & YoutubeLink & "</YoutubeURL>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & "<Status>" & intYoutubeStatus & "</Status>" & vbCrLf
                        ElseIf blnIsYoutubeRemovalOnly Then
                            strNotificationXML += vbTab & vbTab & vbTab & "<IsYoutubeRemovalOnly>" & blnIsYoutubeRemovalOnly.ToString & "</IsYoutubeRemovalOnly>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & "<Status>" & intYoutubeStatus & "</Status>" & vbCrLf
                        Else
                            strNotificationXML += vbTab & vbTab & vbTab & "<YoutubeURL>" & YoutubeLink & "</YoutubeURL>" & vbCrLf
                            strNotificationXML += vbTab & vbTab & vbTab & "<Status>" & intYoutubeStatus & "</Status>" & vbCrLf
                        End If
                        strNotificationXML += vbTab & vbTab & "</Youtube>" & vbCrLf

                        strNotificationXML += vbTab & "</PtSlideShow>" & vbCrLf

                        If intHDMp4Status = 0 And intHDWebmStatus = 0 And intSmartphoneStatus = 0 And intYoutubeStatus = 0 Then

                            arrProcessingList.Add(New Object() {dr.Item("videoid"), 1})

                        ElseIf IsException Then

                            arrProcessingList.Add(New Object() {dr.Item("videoid"), -1})

                        ElseIf IsYoutubeException Then
                            arrProcessingList.Add(New Object() {dr.Item("videoid"), -1})
                        Else
                            arrProcessingList.Add(New Object() {dr.Item("videoid"), -1})
                        End If
                        index += 1
                    Next

                    strNotificationXML += "</PtSlides>" & vbCrLf

                    sw.Write(strNotificationXML.ToString)
                    sw.Close()
                    sw.Dispose()
                    WriteToAppLog("NotificationXML - " & strFilename & " : Updated Successfully")

                    '' Upload XML file on FTP
                    Dim intRetry As Integer = 0
                    Do While intRetry < 3
                        Dim reader As FileStream = Nothing
                        Dim stream As Stream = Nothing
                        Try
                            Dim target As String = "ftp://" & FTPSetting.Host & "/NotificationXMLs/" & strFilename
                            Dim source As String = XMLFileName

                            Dim IsValidate As Boolean = True

                            request = DirectCast(WebRequest.Create(target), FtpWebRequest)
                            request.Method = WebRequestMethods.Ftp.UploadFile
                            request.KeepAlive = True
                            request.Timeout = 1000000 '16 MINUTES
                            request.ReadWriteTimeout = 1000000 '16 MINUTES

                            ''request.Credentials = New NetworkCredential("maxima", "maxinfo2012")
                            Try
                                request.Credentials = New NetworkCredential(FTPSetting.Username, FTPSetting.Password)
                                IsValidate = True
                            Catch ex As Exception
                                WriteToAppLog("Validate FTP Credential With Username Error : " & ex.ToString)
                                IsValidate = False
                            End Try

                            If IsValidate = False Then
                                Try
                                    request.Credentials = New NetworkCredential(FTPSetting.Username1, FTPSetting.Password)
                                    IsValidate = True
                                Catch ex As Exception
                                    WriteToAppLog("Validate FTP Credential With Username1 Error : " & ex.ToString)
                                    IsValidate = False
                                    Throw New Exception("FTP Login Failed Error : " & ex.ToString)
                                End Try
                            End If

                            reader = New FileStream(source, FileMode.Open)
                            Dim data(4096) As Byte

                            Dim buffer(Convert.ToInt32(reader.Length - 1)) As Byte
                            request.ContentLength = buffer.Length

                            stream = request.GetRequestStream
                            Dim intReadBytes As Integer
                            Dim intOffSet As Int64 = 0
                            Do
                                intReadBytes = reader.Read(data, 0, data.Length)
                                intOffSet += intReadBytes
                                stream.Write(data, 0, intReadBytes)

                                Dim intProgress As Int64 = ((intOffSet * 100) / buffer.Length)
                                'DataRow.Cells("status").Value = intProgress
                            Loop While intReadBytes > 0
                            reader.Close() : stream.Close()
                            WriteToAppLog("FeedID : " & FeedID & " - NotificationXML - " & strFilename & " : File Uploaded on FTP")
                            Exit Do
                        Catch ex As Exception
                            WriteToErrorLog("Upload Notification file on FTP Error : Trian No. -" & intRetry & " - " & ex.ToString)
                            WriteToAppLog("Upload Notification file on FTP Error : Trian No. -" & intRetry & " - " & ex.ToString)
                            If intRetry = 2 Then

                                'Deallocation objects before used notification file for attachment
                                If Not reader Is Nothing Then reader.Close() : reader.Dispose()
                                If Not stream Is Nothing Then stream.Close() : stream.Dispose()
                                If Not sw Is Nothing Then sw.Close() : sw.Dispose()

                                Call SendNotificationUploadedFailedEmail(strFilename, ex.Message)
                            End If
                        End Try
                        intRetry += 1
                        If Not request Is Nothing Then request.Abort()
                        If Not reader Is Nothing Then reader.Close() : reader.Dispose()
                        If Not stream Is Nothing Then stream.Close() : stream.Dispose()
                    Loop
                Else
                    WriteToAppLog("Data not Found... NotificationXML File not Created...")
                End If
            Else
                WriteToAppLog("Data not Found... NotificationXML File not Created...")
            End If

        Catch ex As Exception
            WriteToErrorLog("GenerateNotificationXML Procedures : " & ex.ToString)
        End Try
        If conNotificationXML IsNot Nothing Then Call conNotificationXML.Close() : Call conNotificationXML.Dispose()
        If Not sw Is Nothing Then sw.Close() : sw.Dispose()
    End Sub

    Private Sub SendNotificationEmail(ByVal FeedID As Integer)
        Dim conEmailNotification As MySqlConnection = Nothing
        Try
            WriteToAppLog("SendNotificationEmail procedure Starting...")
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conEmailNotification = Database.Connection.Clone
            If Not conEmailNotification.State = ConnectionState.Open Then Call conEmailNotification.Open()

            Dim StrBody As String = ""

            Dim dtIsFeedVideo As DataTable = Database.FetchDataTable("select * from videos where isprocessing=0 and feedid=" & FeedID, Nothing, conEmailNotification)
            If Not dtIsFeedVideo Is Nothing Then
                If dtIsFeedVideo.Rows.Count > 0 Then
                    For Each row As DataRow In dtIsFeedVideo.Rows
                        Dim arrImageFiles As New ArrayList
                        Dim TotalNotDownloadedImage As Integer = 0
                        Dim strYoutubeException As String = ""
                        Dim strVideoError As String = ""
                        Dim OverlayImagePath As String = ""
                        If row.Item("iscompiled") = -1 Or row.Item("iscompiled") = 2 Then

                            Dim strError As String = ""
                            strError = Common.ConvertNull(row.Item("videoerror"), "")
                            If strError.StartsWith("System.Exception: Image Not Found in Directory") Then
                                strVideoError = "Image files are not found"
                                Dim dtImages As DataTable = Database.FetchDataTable("Select imagepath from images where isdownloaded=0 and videoid=" & row.Item("videoid"), Nothing, conEmailNotification)
                                If Not dtImages Is Nothing Then
                                    If dtImages.Rows.Count > 0 Then
                                        TotalNotDownloadedImage = dtImages.Rows.Count
                                        'Dim arrIndex As Integer = 0
                                        For Each imageRow As DataRow In dtImages.Rows
                                            'ReDim Preserve arrImageFile(arrIndex)
                                            'arrImageFile(arrIndex) = imageRow.Item("imagepath")
                                            arrImageFiles.Add(imageRow.Item("imagepath"))
                                            'arrIndex += 1
                                        Next
                                    End If
                                End If
                            ElseIf strError.StartsWith("System.Exception: Video Compiling Failed because Images were not downloaded more then 80%") Then
                                strVideoError = "Video Compiling Failed because Images were not downloaded more then 80%"
                                Dim dtImages As DataTable = Database.FetchDataTable("Select imagepath from images where isdownloaded=0 and videoid=" & row.Item("videoid"), Nothing, conEmailNotification)
                                If Not dtImages Is Nothing Then
                                    If dtImages.Rows.Count > 0 Then
                                        TotalNotDownloadedImage = dtImages.Rows.Count
                                        'Dim arrIndex As Integer = 0
                                        For Each imageRow As DataRow In dtImages.Rows
                                            'ReDim Preserve arrImageFile(arrIndex)
                                            'arrImageFile(arrIndex) = imageRow.Item("imagepath")
                                            arrImageFiles.Add(imageRow.Item("imagepath"))
                                            'arrIndex += 1
                                        Next
                                    End If
                                End If
                            ElseIf strError.Contains("Video not compiled because Overlay Image not downloaded") Then
                                strVideoError = "Video Compiling Failed because Overlay image were not downloaded"
                                OverlayImagePath = row.Item("overlayimagepath")
                            End If
                        Else
                            Dim dtImages As DataTable = Database.FetchDataTable("Select imagepath from images where isdownloaded=0 and videoid=" & row.Item("videoid"), Nothing, conEmailNotification)
                            If Not dtImages Is Nothing Then
                                If dtImages.Rows.Count > 0 Then
                                    TotalNotDownloadedImage = dtImages.Rows.Count
                                    Dim arrIndex As Integer = 0
                                    For Each imageRow As DataRow In dtImages.Rows
                                        'ReDim Preserve arrImageFile(arrIndex)
                                        'arrImageFile(arrIndex) = imageRow.Item("imagepath").ToString
                                        arrImageFiles.Add(imageRow.Item("imagepath"))
                                        ' arrIndex += 1
                                    Next
                                End If
                            End If

                            If row.Item("isyoutubeuploaded") = -1 Or row.Item("isyoutubeuploaded") = 2 Then
                                Dim strYoutubeError As String = ""
                                strYoutubeError = Common.ConvertNull(row.Item("youtubeerror"), "")
                                If strYoutubeError.StartsWith("Google.GData.Client.InvalidCredentialsException: Invalid credentials") Then
                                    strYoutubeException = "Invalid youtube Credentials"
                                Else
                                    strYoutubeException = "Invalid youtube Request"
                                End If
                            End If
                        End If

                        If TotalNotDownloadedImage > 0 Or Not strYoutubeException = "" Or Not strVideoError = "" Then

                            StrBody += "<table border='1' cellpadding='0' cellspacing='0' width='100%'>"
                            StrBody += "<tr><td colspan='2'><b>&nbsp;&nbsp;Property ID : " & row.Item("propertyid") & "</b></td></tr>"
                            If strVideoError = "Image files are not found" Then
                                StrBody += "<tr>"
                                StrBody += "<td width='50%'>"
                                StrBody += "&nbsp;&nbsp;Video Error : Image files were not downloaded"
                                StrBody += "</td>"
                                StrBody += "<td>"
                                StrBody += "&nbsp;&nbsp;List of images<br>"
                                If Not arrImageFiles Is Nothing Then
                                    For i = 0 To arrImageFiles.Count - 1
                                        StrBody += "&nbsp;&nbsp;" & arrImageFiles(i) & "<br>"
                                    Next
                                    'For Each Fname As String In arrImageFile
                                    '    StrBody += "&nbsp;&nbsp;" & Fname & "<br>"
                                    'Next
                                End If
                                StrBody += "</td>"
                                StrBody += "</tr>"
                            ElseIf strVideoError = "Video Compiling Failed because Images were not downloaded more then 80%" Then
                                StrBody += "<tr>"
                                StrBody += "<td width='50%'>"
                                StrBody += "&nbsp;&nbsp;Video Error : Video Compiling Failed because Images were not downloaded more then 80%"
                                StrBody += "</td>"
                                StrBody += "<td>"
                                StrBody += "&nbsp;&nbsp;List of images<br>"
                                If Not arrImageFiles Is Nothing Then
                                    For i = 0 To arrImageFiles.Count - 1
                                        StrBody += "&nbsp;&nbsp;" & arrImageFiles(i) & "<br>"
                                    Next
                                    'For Each Fname As String In arrImageFile
                                    '    StrBody += "&nbsp;&nbsp;" & Fname & "<br>"
                                    'Next
                                End If
                                StrBody += "</td>"
                                StrBody += "</tr>"
                                'ElseIf strVideoError = "Video compiling error" Then
                                '    StrBody += "<tr>"
                                '    StrBody += "<td width='50%'><br>"
                                '    StrBody += "&nbsp;&nbsp;Video Error : Video compiling error"
                                '    StrBody += "<br></td>"
                                '    StrBody += "<td><br>"
                                '    StrBody += "&nbsp;&nbsp;System retry to fix error"
                                '    StrBody += "<br></td>"
                                '    StrBody += "</tr>"
                            ElseIf strVideoError = "Video Compiling Failed because Overlay image were not downloaded" Then

                                StrBody += "<tr>"
                                StrBody += "<td width='50%'><br>"
                                StrBody += "&nbsp;&nbsp;Video Compiling Failed because Overlay image were not downloaded."
                                StrBody += "<br></td>"
                                StrBody += "<td><br>"
                                StrBody += "&nbsp;&nbsp;" & OverlayImagePath
                                StrBody += "<br></td>"
                                StrBody += "</tr>"

                            ElseIf TotalNotDownloadedImage > 0 Then
                                StrBody += "<tr>"
                                StrBody += "<td width='50%'>"
                                StrBody += "&nbsp;&nbsp;Video compiled successfully but some images were not downloaded"
                                StrBody += "</td>"
                                StrBody += "<td>"
                                StrBody += "&nbsp;&nbsp;List of images<br>"
                                If Not arrImageFiles Is Nothing Then
                                    For i = 0 To arrImageFiles.Count - 1
                                        StrBody += "&nbsp;&nbsp;" & arrImageFiles(i) & "<br>"
                                    Next
                                    ''For Each Fname As String In arrImageFile
                                    ''    StrBody += "&nbsp;&nbsp;" & Fname & "<br>"
                                    ''Next
                                End If
                                StrBody += "</td>"
                                StrBody += "</tr>"
                            End If

                            If Not strYoutubeException = "" Then
                                If strYoutubeException = "Invalid youtube Credentials" Then

                                    Dim dtUserDetails As DataTable = Database.FetchDataTable("SELECT * FROM users where userid=" & row.Item("userid"), Nothing, conEmailNotification)

                                    Dim Username As String = ""
                                    Dim Password As String = ""
                                    Dim Email As String = ""

                                    If Not dtUserDetails Is Nothing Then
                                        If Common.IsNull(dtUserDetails.Rows(0).Item("youtubeusername")) Then
                                            Username = Common.ConvertNull(dtUserDetails.Rows(0).Item("youtubeusername"), "")
                                        Else
                                            Username = dtUserDetails.Rows(0).Item("youtubeusername")
                                        End If

                                        If Common.IsNull(dtUserDetails.Rows(0).Item("youtubepassword")) Then
                                            Password = Common.ConvertNull(dtUserDetails.Rows(0).Item("youtubepassword"), "")
                                        Else
                                            Password = dtUserDetails.Rows(0).Item("youtubepassword")
                                        End If

                                        If Common.IsNull(dtUserDetails.Rows(0).Item("email")) Then
                                            Email = Common.ConvertNull(dtUserDetails.Rows(0).Item("email"), "")
                                        Else
                                            Email = dtUserDetails.Rows(0).Item("email")
                                        End If
                                    End If

                                    StrBody += "<tr>"
                                    StrBody += "<td width='50%'><br>"
                                    StrBody += "&nbsp;&nbsp;Invalid youtube Credentials<br><br>"
                                    StrBody += "&nbsp;&nbsp;Youtube Email Address<br>"
                                    StrBody += "&nbsp;&nbsp;Youtube Username<br>"
                                    StrBody += "&nbsp;&nbsp;Youtube Password<br>"
                                    StrBody += "<br></td>"
                                    StrBody += "<td>"
                                    StrBody += "<br><br>"
                                    StrBody += "&nbsp;&nbsp;" & Email & "<br>"
                                    StrBody += "&nbsp;&nbsp;" & Username & "<br>"
                                    StrBody += "&nbsp;&nbsp;" & Password & "<br>"
                                    StrBody += "<br></td>"
                                    StrBody += "</tr>"
                                ElseIf strYoutubeException = "Invalid youtube Request" Then

                                    StrBody += "<tr>"
                                    StrBody += "<td width='50%'><br>"
                                    StrBody += "&nbsp;&nbsp;Youtube uploading failed"
                                    StrBody += "<br></td>"
                                    StrBody += "<td><br>"
                                    StrBody += "&nbsp;&nbsp;Invalid youtube Request"
                                    StrBody += "<br></td>"
                                    StrBody += "</tr>"
                                End If
                            End If
                            StrBody += "</table><br>"
                        End If
                    Next
                    WriteToAppLog("SendNotificationEmail Email body Created...")
                    If Not StrBody = "" Then
                        If SendEmail(StrBody) Then
                            WriteToAppLog("Feed ID : " & FeedID & " - Notification Email Send successfully")
                        Else
                            WriteToAppLog("Notification Email Sending failed")
                        End If
                    Else
                        WriteToAppLog("There are no any videos were failed in this batch")
                    End If
                End If
            End If
            WriteToAppLog("SendNotificationEmail procedure ending...")
        Catch ex As Exception
            WriteToAppLog("SendEmailNotification Procedure : " & ex.ToString)
            WriteToErrorLog("SendEmailNotification Procedure : " & ex.ToString)
        End Try
        If conEmailNotification IsNot Nothing Then Call conEmailNotification.Close() : Call conEmailNotification.Dispose()
    End Sub


    Private Sub SendXMLUpdationFailedEmail(ByVal Exception As String)
        Try
            Dim strBody As String = ""
            StrBody += "<table border='0' cellpadding='0' cellspacing='0' width='100%'>"
            strBody += "<tr><td><b>Failed to update feed due to below error</b></td></tr>"
            strBody += "<tr><td><br></td></tr>"
            strBody += "<tr><td>Exception : " & Exception.ToString & "</td></tr>"
            strBody += "</table>"

            If SendEmail(strBody) Then
                WriteToAppLog("XML updation failed Notification Email Send successfully")
            Else
                WriteToAppLog("XML updation failed Notification Email Sending failed")
            End If

        Catch ex As Exception
            WriteToAppLog("SendXMLUpdationFailedEmail Procedure : " & ex.ToString)
        End Try
    End Sub

    Private Sub SendNotificationUploadedFailedEmail(ByVal strFilename As String, ByVal Exception As String)
        Try
            Dim strBody As String = ""
            strBody += "<table border='0' cellpadding='0' cellspacing='0' width='100%'>"
            strBody += "<tr><td><b>Failed to upload Notification file on FTP due to below error</b></td></tr>"
            strBody += "<tr><td><br></td></tr>"
            strBody += "<tr><td>Exception : " & Exception.ToString & "</td></tr>"
            strBody += "</table>"

            If SendEmail(strBody, strFilename) Then
                WriteToAppLog("XML updation failed Notification Email Send successfully")
            Else
                WriteToAppLog("XML updation failed Notification Email Sending failed")
            End If

        Catch ex As Exception
            WriteToAppLog("SendNotificationUploadedFailedEmail Procedure : " & ex.ToString)
        End Try
    End Sub


    Private Function SendEmail(ByVal Body As String, Optional ByVal AttachedFileName As String = "") As Boolean

        WriteToAppLog("SendEmail procedure starting...")
        Dim emailMessage As New MailMessage()
        Dim SMTPServer As New SmtpClient()
        Dim stream As FileStream = Nothing
        Dim blnSuccess As Boolean = False
        Try
            SMTPServer = New SmtpClient("smtp.gmail.com", 25)

            SMTPServer.Credentials = New Net.NetworkCredential("propertytubeservice@gmail.com", "maximaa2013")
            SMTPServer.EnableSsl = True

            Dim StrBody As String = ""
            emailMessage = New MailMessage()
            emailMessage.From = New MailAddress("propertytubeservice@gmail.com")
            'emailMessage.To.Add("Gareth@propertytube.com,Bryan@propertytube.com")
            emailMessage.To.Add("Bryan@propertytube.com")
            emailMessage.Bcc.Add("hiren@maximaainfoways.com")
            emailMessage.Subject = "Property Tube Notification Email"
            emailMessage.IsBodyHtml = True

            Dim XMLFileName As String = Application.StartupPath & "\NotificationXML\" & AttachedFileName
            If File.Exists(XMLFileName) Then
                stream = File.OpenRead(XMLFileName)
                Dim attachment As Attachment = New Attachment(stream, AttachedFileName)
                emailMessage.Attachments.Add(attachment)
            End If
            emailMessage.Body = Body

            SMTPServer.Send(emailMessage)
            blnSuccess = True
            WriteToAppLog("SendEmail Procedure : Email Send Successfully")
        Catch ex As Exception
            WriteToAppLog("SendEmail Function : " & ex.ToString)
            WriteToErrorLog("SendEmail Function : " & ex.ToString)
            blnSuccess = False
        End Try

        If stream IsNot Nothing Then stream.Close()
        WriteToAppLog("SendEmail procedure Ending...")
        emailMessage = Nothing
        SMTPServer = Nothing
        Return blnSuccess
    End Function

End Class
