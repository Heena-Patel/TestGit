Imports System.Threading
Imports MySql.Data
Imports MySql.Data.MySqlClient
Imports Google.YouTube
Imports Google.GData.Client
Imports Google.GData.Extensions.MediaRss
Imports Google.GData.YouTube
Imports Amazon.S3
Imports Amazon.S3.Model
Imports Amazon.S3.IO
Imports System.IO
Imports System.Net


Public Class Uploading
    Implements QueueItem

#Region "Variables and Constants"

    Private intVideoID As Integer
    Private blnMakeHDVideo As Boolean = True

#End Region

    Public Sub New(ByVal VideoID As Integer)
        intVideoID = VideoID
    End Sub

    Public Event ProcessCompleted(ByVal item As QueueItem) Implements QueueItem.ProcessCompleted

    Public Event ProcessNotCompleted(ByVal item As QueueItem, ByVal exception As System.Exception) Implements QueueItem.ProcessNotCompleted

    Public Sub StartProcess() Implements QueueItem.StartProcess
        Dim thrdUploadProcess As Thread = New Thread(AddressOf UploadProcessing)
        thrdUploadProcess.Start()
    End Sub

    Private Sub UploadProcessing()
        Dim conYoutubeProcess As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conYoutubeProcess = Database.Connection.Clone
            If Not conYoutubeProcess.State = ConnectionState.Open Then Call conYoutubeProcess.Open()


            Dim strStatement As String = "SELECT ifnull(users.country, '') as country FROM users " & _
                                        "INNER JOIN videos ON users.userid = videos.userid " & _
                                        "WHERE videos.videoid=" & intVideoID

            Dim strCountry As String = Database.ExecuteSQL(strStatement, Nothing, conYoutubeProcess)
            If strCountry = "za" Then
                blnMakeHDVideo = False
            Else
                blnMakeHDVideo = True
            End If

            Dim dtUploadStatus As DataTable = Database.FetchDataTable("select * from videos where videoid=" & intVideoID, Nothing, conYoutubeProcess)
            If Not dtUploadStatus Is Nothing Then
                If dtUploadStatus.Rows.Count > 0 Then
                    If dtUploadStatus.Rows(0).Item("iscompiled") = 1 Then
                        If dtUploadStatus.Rows(0).Item("youtubeuploadonly") = True Then
                            WriteToAppLog("Video ID : " & intVideoID & " - YoutubeUploadOnly video is processing...")
                            If DownloadFileFromAmazon() Then
                                Call YoutubeUploading()
                            Else
                                WriteToAppLog("Error - Downloading Failed in Video ID : " & intVideoID)
                                Dim strError As String = "Error in youtubeuploadonly - Downloading Failed in Video ID : " & intVideoID
                                Call UpdateYoutubeError(strError, intVideoID)
                                Throw New Exception("Error in youtubeuploadonly - Downloading Failed in Video ID : " & intVideoID)
                            End If

                        ElseIf dtUploadStatus.Rows(0).Item("youtuberemovalonly") = True Then
                            WriteToAppLog("Video ID : " & intVideoID & " - YoutubeRemovalOnly video is processing...")
                            Call RemoveYoutubeVideo()
                        End If

                        If dtUploadStatus.Rows(0).Item("isyoutubeuploaded") = 0 Or dtUploadStatus.Rows(0).Item("isyoutubeuploaded") = -1 Then
                            Call YoutubeUploading()
                        End If

                        'Upload Video on FTP
                        If dtUploadStatus.Rows(0).Item("isuploaded") = 0 Or dtUploadStatus.Rows(0).Item("isuploaded") = -1 Then
                            Call UploadVideoOnFTP()
                        End If

                        If dtUploadStatus.Rows(0).Item("iswebmvideo") = 0 Or dtUploadStatus.Rows(0).Item("iswebmvideo") = -1 Then
                            Call ConvertVideoForWebm()
                        End If

                        If dtUploadStatus.Rows(0).Item("issmartphonevideo") = 0 Or dtUploadStatus.Rows(0).Item("issmartphonevideo") = -1 Then
                            Call ConvertVideoForMobile()
                        End If

                        If dtUploadStatus.Rows(0).Item("ismp4videouploaded") = 0 Or dtUploadStatus.Rows(0).Item("ismp4videouploaded") = -1 Then
                            Call UploadMP4VideoOnAmazone()
                        End If

                        If dtUploadStatus.Rows(0).Item("iswebmvideouploaded") = 0 Or dtUploadStatus.Rows(0).Item("iswebmvideouploaded") = -1 Then
                            Call UploadWebmVideoOnAmazone()
                        End If

                        If dtUploadStatus.Rows(0).Item("issmartphonevideouploaded") = 0 Or dtUploadStatus.Rows(0).Item("issmartphonevideouploaded") = -1 Then
                            Call UploadSmartphoneVideoOnAmazone()
                        End If

                        If dtUploadStatus.Rows(0).Item("isuploadmp4thumb") = 0 Or dtUploadStatus.Rows(0).Item("isuploadmp4thumb") = -1 Then
                            Call UploadMp4ThumbnailOnAmazone()
                        End If

                        If dtUploadStatus.Rows(0).Item("isuploadsmartphonethumb") = 0 Or dtUploadStatus.Rows(0).Item("isuploadsmartphonethumb") = -1 Then
                            Call UploadSmartphoneThumbnailOnAmazone()
                        End If
                    Else
                        WriteToAppLog("VideoID : " & intVideoID & " All Uploading failed because video is not compiled.")
                    End If
                End If
            End If

            '' Code for Remove VideoCache Folder
            Dim intIsRetryVideo As Integer = Database.ExecuteSQL("select count(*) from videos where (isuploaded=0 or isuploaded=-1 or isyoutubeuploaded=0 or isyoutubeuploaded=-1 or ismp4videouploaded=-1 or ismp4videouploaded=0 or iswebmvideo=-1 or iswebmvideo=0 or iswebmvideouploaded=-1 or iswebmvideouploaded=0 or issmartphonevideo=-1 or issmartphonevideo=0 or issmartphonevideouploaded=-1 or issmartphonevideouploaded=0 or isuploadmp4thumb=-1 or isuploadmp4thumb=0  or isuploadsmartphonethumb=-1 or isuploadsmartphonethumb=0) and videoid=" & intVideoID, Nothing, conYoutubeProcess)
            If intIsRetryVideo = 0 Then
                Dim strVideoFilePath As String = ""
                strVideoFilePath = Common.ConvertNull(Database.ExecuteSQL("select videopath from videos where videoid=" & intVideoID, Nothing, conYoutubeProcess), "")
                If strVideoFilePath = "" Then
                    WriteToAppLog("VideoID : " & intVideoID & " - Videocache Folder not deletedVideo, Videocache Filepath not found in database")
                Else
                    Dim strVideoCachePath As String = strVideoFilePath.Substring(0, strVideoFilePath.Length - 9)
                    Directory.Delete(strVideoCachePath, True)
                    WriteToAppLog("VideoID : " & intVideoID & " Videocache Folder deleted.")
                End If
            End If
            RaiseEvent ProcessCompleted(Me)
        Catch ex As Exception
            RaiseEvent ProcessNotCompleted(Me, ex)
            WriteToErrorLog("UploadProcessing Procedure : " & ex.ToString)
        End Try
        If conYoutubeProcess IsNot Nothing Then conYoutubeProcess.Close() : conYoutubeProcess.Dispose()
    End Sub

    Private Function DownloadFileFromAmazon() As Boolean
        Dim conDownloadAmazon As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conDownloadAmazon = Database.Connection.Clone
            If Not conDownloadAmazon.State = ConnectionState.Open Then Call conDownloadAmazon.Open()


            Dim dtBucketinfo As DataTable = Database.FetchDataTable("select videoname, bucket, foldername from videos where videoid=" & intVideoID, Nothing, conDownloadAmazon)
            If Not dtBucketinfo Is Nothing Then
                If dtBucketinfo.Rows.Count > 0 Then

                    Dim BucketName As String = ""
                    If Common.IsNull(dtBucketinfo.Rows(0).Item("bucket")) Then
                        BucketName = Common.ConvertNull(dtBucketinfo.Rows(0).Item("bucket"), "")
                    Else
                        BucketName = dtBucketinfo.Rows(0).Item("bucket")
                    End If

                    Dim FolderName As String = ""
                    If Common.IsNull(dtBucketinfo.Rows(0).Item("foldername")) Then
                        FolderName = Common.ConvertNull(dtBucketinfo.Rows(0).Item("foldername"), "")
                    Else
                        FolderName = dtBucketinfo.Rows(0).Item("foldername") & "/"
                    End If

                    Dim VideoName As String = ""
                    If Common.IsNull(dtBucketinfo.Rows(0).Item("videoname")) Then
                        VideoName = Common.ConvertNull(dtBucketinfo.Rows(0).Item("videoname"), "")
                    Else
                        VideoName = dtBucketinfo.Rows(0).Item("videoname") & ".mp4"
                    End If

                    If BucketName = "" Then
                        Throw New Exception("BucketName not found in Video ID - " & intVideoID)
                    End If
                    If FolderName = "" Then
                        Throw New Exception("FolderName not found in Video ID - " & intVideoID)
                    End If
                    If VideoName = "" Then
                        Throw New Exception("Download FileName not found in Video ID - " & intVideoID)
                    End If

                    Dim Client As AmazonS3
                    Client = New AmazonS3Client(AWS_ACCESS_KEY, AWS_SECRET_KEY)

                    Dim objGetObjectRequest As GetObjectRequest
                    objGetObjectRequest = New GetObjectRequest
                    objGetObjectRequest.BucketName = BucketName
                    objGetObjectRequest.Key = FolderName & VideoName
                    objGetObjectRequest.Timeout = 3600000 ' 1 hour
                    'objGetObjectRequest.ReadWriteTimeout=1000000

                    Dim objResponse As New GetObjectResponse
                    objResponse = Client.GetObject(objGetObjectRequest)

                    If Not objResponse Is Nothing Then

                        If Not Directory.Exists(Application.StartupPath & "/Download") Then
                            Directory.CreateDirectory(Application.StartupPath & "/Download")
                        End If

                        If Not Directory.Exists(Application.StartupPath & "/Download/" & intVideoID) Then
                            Directory.CreateDirectory(Application.StartupPath & "/Download/" & intVideoID)
                        End If

                        Dim ImageStream As Stream = objResponse.ResponseStream
                        Dim file As MemoryStream = Nothing

                        Dim read(4096) As Byte
                        Dim count As Int32 = ImageStream.Read(read, 0, read.Length)
                        file = New MemoryStream
                        Do While (count > 0)
                            file.Write(read, 0, count)
                            count = ImageStream.Read(read, 0, read.Length)
                        Loop
                        file.Position = 0
                        ImageStream.Close()
                        ImageStream.Dispose()
                        objResponse.Dispose()

                        Dim FileName As String = ""
                        FileName = Application.StartupPath & "/Download/" & intVideoID & "/" & VideoName
                        Dim Stream As FileStream = New FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)
                        count = file.Read(read, 0, read.Length)
                        Do While (count > 0)
                            Stream.Write(read, 0, count)
                            count = file.Read(read, 0, read.Length)
                        Loop
                        Stream.Close()
                        Stream.Dispose()
                        file.Close()

                        'Dim FileName As String = ""
                        'FileName = Application.StartupPath & "/Download/" & intVideoId & "/" & VideoName
                        'Dim Stream As FileStream = New FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read, 8, FileOptions.WriteThrough)

                        'Dim data(4096) As Byte
                        'Dim intOffset As Int64 = 0
                        'Dim intReadBytes As Integer
                        ''Dim TotalBytes As Byte = ResponseStream.Length
                        'Do
                        '    intReadBytes = ImageStream.Read(data, 0, data.Length)
                        '    intOffset += intReadBytes
                        '    Stream.Write(data, 0, intReadBytes)
                        'Loop While intReadBytes > 0

                        'Stream.Close()
                        'Stream.Dispose()


                        WriteToAppLog("Video ID - " & intVideoID & " Successfully Downloaded from Amazon..!!!")
                        DownloadFileFromAmazon = True
                    Else
                        Throw New Exception("Error : Video Downloading Failed From Amazone in Video ID - " & intVideoID)
                    End If
                End If
            End If

        Catch ex As Exception
            DownloadFileFromAmazon = False
            WriteToErrorLog("DownloadFileFromAmazon Function : " & ex.ToString)
            WriteToAppLog("DownloadFileFromAmazon Function : " & ex.Message)
        End Try
        If conDownloadAmazon IsNot Nothing Then conDownloadAmazon.Close() : conDownloadAmazon.Dispose()
    End Function

    Private Sub YoutubeUploading()
        Dim conYoutube As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conYoutube = Database.Connection.Clone
            If Not conYoutube.State = ConnectionState.Open Then Call conYoutube.Open()

            'Dim DeveloperKey As String = "AI39si6o1rBnAmoiMKhQDeVv1Vyp1JfMc8ZLJJVHQJGqVoFeBHaxmxs-6G7Jk21lq45qS_I7EX5pp7pXXXjFSXP61hLJRrRAww"
            'Dim Username As String = "PropertyTube1"
            'Dim Password As String = "pokerboy"

            Dim DeveloperKey As String = ""
            Dim Username As String = ""
            Dim Password As String = ""
            Dim intUserID As Integer
            Dim VideoName As String = ""
            Dim IsYoutubeOnly As Integer = -1

            Dim dtYoutube As DataTable = Database.FetchDataTable("select * from videos where iscompiled=1 and (isyoutubeuploaded=0 or isyoutubeuploaded=-1) and videoid=" & intVideoID, Nothing, conYoutube)
            If Not dtYoutube Is Nothing Then
                If dtYoutube.Rows.Count > 0 Then

                    intUserID = dtYoutube.Rows(0).Item("userid")
                    VideoName = dtYoutube.Rows(0).Item("videoname")

                    Dim dtUserDetails As DataTable = Database.FetchDataTable("SELECT * FROM users where userid=" & intUserID, Nothing, conYoutube)

                    If Not dtUserDetails Is Nothing Then
                        If Common.IsNull(dtUserDetails.Rows(0).Item("youtubeusername")) Then
                            Username = Common.ConvertNull(dtUserDetails.Rows(0).Item("youtubeusername"), "")
                        Else
                            Username = dtUserDetails.Rows(0).Item("youtubeusername")
                            Username = Username.Trim
                        End If

                        If Common.IsNull(dtUserDetails.Rows(0).Item("youtubepassword")) Then
                            Password = Common.ConvertNull(dtUserDetails.Rows(0).Item("youtubepassword"), "")
                        Else
                            Password = dtUserDetails.Rows(0).Item("youtubepassword")
                            Password = Password.Trim
                        End If

                        If Common.IsNull(dtUserDetails.Rows(0).Item("youtubeapikey")) Then
                            DeveloperKey = Common.ConvertNull(dtUserDetails.Rows(0).Item("youtubeapikey"), "")
                        Else
                            DeveloperKey = dtUserDetails.Rows(0).Item("youtubeapikey")
                            DeveloperKey = DeveloperKey.Trim
                        End If
                    End If


                    Dim dtVerify As DataTable = Database.FetchDataTable("select * from videos where videoname='" & VideoName & "' ", Nothing, conYoutube)
                    If Not dtVerify Is Nothing Then
                        Dim YoutubeLink As String = ""
                        For Each drRow As DataRow In dtVerify.Rows
                            If Common.IsNull(drRow.Item("youtubelink")) Then
                                YoutubeLink = Common.ConvertNull(drRow.Item("youtubelink"), "")
                            Else
                                YoutubeLink = drRow.Item("youtubelink")
                            End If

                            If Not YoutubeLink = "" Then
                                Dim settings1 As YouTubeRequestSettings = New YouTubeRequestSettings("PropertyTube", DeveloperKey, Username, Password)
                                Dim request As YouTubeRequest = New YouTubeRequest(settings1)

                                Dim arrYoutubeLink As Array = YoutubeLink.Split("?")
                                If arrYoutubeLink IsNot Nothing Then
                                    Try
                                        Dim arrYoutube As Array = arrYoutubeLink(1).ToString.Split("&")
                                        If arrYoutube IsNot Nothing Then
                                            Dim arrYoutubeVideoid As Array = arrYoutube(0).ToString.Split("=")
                                            Dim videoID As String = arrYoutubeVideoid(1)
                                            Dim videoEntryUrl As Uri = New Uri("http://gdata.youtube.com/feeds/api/users/default/uploads/" & videoID)
                                            Dim Retrivevideo As Video = request.Retrieve(Of Video)(videoEntryUrl)
                                            request.Delete(Retrivevideo)
                                        End If
                                        WriteToAppLog("Video ID : " & intVideoID & " - Duplicate Video Deleted on Youtube")
                                    Catch ex As Exception
                                        WriteToAppLog("Video ID : " & intVideoID & " - Error in Delete Duplicate Video on Youtube")
                                        WriteToErrorLog("Video ID : " & intVideoID & " - Error in Delete Duplicate Video on Youtube")
                                    End Try

                                End If

                                'Dim vFeed As Feed(Of Video) = request.GetVideoFeed("default")
                                'For Each video As Video In vFeed.Entries
                                '    If video.WatchPage.AbsoluteUri = YoutubeLink.ToString() Then
                                '        request.Delete(Of Video)(video)
                                '        Exit For
                                '    End If
                                'Next
                            End If
                        Next
                    End If
                    Dim VideoFilePath As String = ""

                    If dtYoutube.Rows(0).Item("youtubeuploadonly") = True Then
                        VideoFilePath = Application.StartupPath & "\Download\" & intVideoID & "\" & VideoName & ".mp4"
                        IsYoutubeOnly = 1
                    Else
                        VideoFilePath = dtYoutube.Rows(0).Item("videopath").ToString
                    End If

                    If File.Exists(VideoFilePath) Then
                        Dim settings As YouTubeRequestSettings = New YouTubeRequestSettings("PropertyTube", DeveloperKey, Username, Password)
                        Dim request As YouTubeRequest = New YouTubeRequest(settings)

                        DirectCast(request.Service.RequestFactory, GDataRequestFactory).Timeout = 9999999
                        Dim uploadVideo As New Video

                        Dim strYoutubeTitle As String = dtYoutube.Rows(0).Item("youtubetitle")
                        If strYoutubeTitle.Length > 100 Then 'Max 100 Character of YoutubeTitle
                            Dim StripLength As Integer = (strYoutubeTitle.Length) - 100
                            strYoutubeTitle = strYoutubeTitle.Remove(strYoutubeTitle.Length - StripLength)
                        End If

                        Dim strDescription As String = ""
                        If Common.IsNull(dtYoutube.Rows(0).Item("youtubedescription")) Then
                            strDescription = Common.ConvertNull(dtYoutube.Rows(0).Item("youtubedescription"), "")
                        Else
                            strDescription = dtYoutube.Rows(0).Item("youtubedescription")
                        End If
                        If strDescription.Length > 5000 Then
                            Dim StripDescLength As Integer = (strDescription.Length) - 5000
                            strDescription = strDescription.Remove(strDescription.Length - StripDescLength)
                        End If

                        Dim strKeywords As String
                        If Common.IsNull(dtYoutube.Rows(0).Item("youtubekeywords")) Then
                            strKeywords = Common.ConvertNull(dtYoutube.Rows(0).Item("youtubekeywords"), "")
                        Else
                            strKeywords = dtYoutube.Rows(0).Item("youtubekeywords")
                        End If
                        If strKeywords.Length > 500 Then
                            Dim StripKeywordLength As Integer = (strKeywords.Length) - 500
                            strKeywords = strKeywords.Remove(strKeywords.Length - StripKeywordLength)
                        End If

                        uploadVideo.Title = strYoutubeTitle
                        uploadVideo.Description = strDescription
                        uploadVideo.Keywords = strKeywords
                        uploadVideo.Tags.Add(New MediaCategory("Entertainment", YouTubeNameTable.CategorySchema))
                        uploadVideo.YouTubeEntry.Private = False
                        uploadVideo.YouTubeEntry.MediaSource = New MediaFileSource(VideoFilePath, "video/mp4")
                        'Dim video As Video = request.Upload(Username, uploadVideo)
                        Dim video As Video = request.Upload(uploadVideo)
                        Dim YoutubeLink As String = video.WatchPage.AbsoluteUri
                        Dim YoutubeVideoID As String = video.VideoId

                        Database.ExecuteSQL("update videos set youtubelink='" & YoutubeLink & "', isyoutubeuploaded=1, youtubevideoid='" & YoutubeVideoID & "', isyoutubeonly='" & IsYoutubeOnly & "' where videoid=" & intVideoID, Nothing, conYoutube)
                        WriteToAppLog("VideoID " & intVideoID & " is uploaded sucessfully on Youtube...")
                    Else
                        WriteToAppLog("VideoID " & intVideoID & " File Not Found for Upload")
                        WriteToErrorLog("VideoID " & intVideoID & " File Not Found for Upload")
                        Throw New Exception("VideoID " & intVideoID & " File Not Found for Upload")
                    End If
                End If
            End If
        Catch ex As Exception
            WriteToErrorLog("YoutubeUploading Procedures : " & ex.ToString)
            Call UpdateYoutubeError(ex.ToString, intVideoID)
        End Try
        If conYoutube IsNot Nothing Then conYoutube.Close() : conYoutube.Dispose()
    End Sub

    Private Sub UpdateYoutubeError(ByVal strerror As String, ByVal videoid As Integer)
        If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
        Dim conYoutubeError As MySqlConnection = Database.Connection.Clone
        If Not conYoutubeError.State = ConnectionState.Open Then Call conYoutubeError.Open()
        Dim cmdYoutubeError As New MySqlCommand("update videos set youtubeerror=?youtubeerror, isyoutubeuploaded=?isyoutubeuploaded, isyoutubeonly=?isyoutubeonly where videoid=?videoid", conYoutubeError)
        With cmdYoutubeError
            With .Parameters
                Call .AddWithValue("youtubeerror", strerror)
                Call .AddWithValue("isyoutubeuploaded", 2)
                Call .AddWithValue("isyoutubeonly", -1)
                Call .AddWithValue("videoid", videoid)
            End With
            Call .ExecuteNonQuery()
        End With
        conYoutubeError.Close() : conYoutubeError.Dispose()
    End Sub

    Private Sub RemoveYoutubeVideo()
        Dim conYoutubeRemove As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conYoutubeRemove = Database.Connection.Clone
            If Not conYoutubeRemove.State = ConnectionState.Open Then Call conYoutubeRemove.Open()

            Dim DeveloperKey As String = ""
            Dim Username As String = ""
            Dim Password As String = ""
            Dim intUserID As Integer
            Dim VideoName As String = ""
            Dim intPropertyId As Integer = 0
            Dim strYoutubeVideoID As String = ""

            Dim dtYoutube As DataTable = Database.FetchDataTable("select * from videos where videoid=" & intVideoID, Nothing, conYoutubeRemove)
            If Not dtYoutube Is Nothing Then
                If dtYoutube.Rows.Count > 0 Then

                    intUserID = dtYoutube.Rows(0).Item("userid")
                    'VideoName = dtYoutube.Rows(0).Item("videoname")
                    intPropertyId = dtYoutube.Rows(0).Item("propertyid")

                    If Not intPropertyId > 0 Then
                        Throw New Exception("Property ID is must be greater then Zero")
                    End If
                    Dim dtUserDetails As DataTable = Database.FetchDataTable("SELECT * FROM users where userid=" & intUserID, Nothing, conYoutubeRemove)
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

                        If Common.IsNull(dtUserDetails.Rows(0).Item("youtubeapikey")) Then
                            DeveloperKey = Common.ConvertNull(dtUserDetails.Rows(0).Item("youtubeapikey"), "")
                        Else
                            DeveloperKey = dtUserDetails.Rows(0).Item("youtubeapikey")
                        End If
                    End If

                    Dim dtVerify As DataTable = Database.FetchDataTable("select * from videos where propertyid='" & intPropertyId & "' ", Nothing, conYoutubeRemove)
                    If Not dtVerify Is Nothing Then
                        Dim YoutubeLink As String = ""
                        For Each drRow As DataRow In dtVerify.Rows

                            If Common.IsNull(drRow.Item("youtubevideoid")) Then
                                strYoutubeVideoID = ""
                            Else
                                strYoutubeVideoID = drRow.Item("youtubevideoid")
                            End If

                            If Not strYoutubeVideoID = "" Then
                                Dim settings1 As YouTubeRequestSettings = New YouTubeRequestSettings("PropertyTube", DeveloperKey, Username, Password)
                                Dim request As YouTubeRequest = New YouTubeRequest(settings1)

                                Try
                                    Dim videoEntryUrl As Uri = New Uri("http://gdata.youtube.com/feeds/api/users/default/uploads/" & strYoutubeVideoID)
                                    Dim Retrivevideo As Video = request.Retrieve(Of Video)(videoEntryUrl)
                                    request.Delete(Retrivevideo)

                                    WriteToAppLog("Video ID : " & intVideoID & " - Duplicate Video Deleted on Youtube")
                                Catch ex As Exception
                                    WriteToAppLog("Video ID : " & intVideoID & " - Error in Delete Duplicate Video on Youtube")
                                    WriteToErrorLog("Video ID : " & intVideoID & " - Error in Delete Duplicate Video on Youtube")
                                End Try

                                'Dim vFeed As Feed(Of Video) = request.GetVideoFeed("default")
                                'For Each video As Video In vFeed.Entries
                                '    If video.WatchPage.AbsoluteUri = YoutubeLink.ToString() Then
                                '        request.Delete(Of Video)(video)
                                '        Exit For
                                '    End If
                                'Next
                            End If
                        Next
                        Dim cmdYoutubeRemoval As New MySqlCommand("update videos set isyoutubeonly=?isyoutubeonly where videoid=?videoid", conYoutubeRemove)
                        With cmdYoutubeRemoval
                            With .Parameters
                                Call .AddWithValue("isyoutubeonly", 1)
                                Call .AddWithValue("videoid", intVideoID)
                            End With
                            Call .ExecuteNonQuery()
                        End With
                        WriteToAppLog("Video ID : " & intVideoID & " - YoutubeRemovalOnly Videos Deleted Successfully on Youtube")
                    End If
                End If
            End If

        Catch ex As Exception
            WriteToErrorLog("RemoveYoutubeVideo Procedures : " & ex.ToString)
            Call UpdateYoutubeError(ex.ToString, intVideoID)
        End Try
        If conYoutubeRemove IsNot Nothing Then conYoutubeRemove.Close() : conYoutubeRemove.Dispose()
    End Sub

    Private Sub UploadVideoOnFTP()
        Dim conFTPConnection As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conFTPConnection = Database.Connection.Clone
            If Not conFTPConnection.State = ConnectionState.Open Then Call conFTPConnection.Open()

            Dim intUserID As Integer = 0
            Dim strVideoName As String = ""
            Dim strVideoPath As String = ""
            Dim strPropertyid As String = ""
            Dim drData As DataTableReader = Database.FetchDataReader("SELECT userid, videoname, videopath, propertyid  FROM videos WHERE videoid=" & intVideoID, Nothing, conFTPConnection)
            If drData.Read Then
                intUserID = drData.Item("userid")
                strVideoName = drData.Item("videoname")
                strVideoPath = drData.Item("videopath")
                strPropertyid = drData.Item("propertyid")
            Else
                Throw New Exception("Unable to find video details")
            End If
            If intUserID > 0 Then
                If MakeDirectory(intUserID) Then
                    If File.Exists(strVideoPath) Then
                        'Dim FtpPath As String = "ftp://" & FTPSetting.Host & "/Videos/" & intUserID & "/" & strVideoName & ".mp4"
                        Dim FtpPath As String = "ftp://" & FTPSetting.Host & "/Videos/" & intUserID & "/" & strPropertyid & ".mp4"
                        Dim CompleteFilePath As String = "ftp://" & FTPSetting.Host & "/Videos/" & intUserID & "/" & strPropertyid & ".mp4.complete"
                        If UploadVideoToFTP(strVideoPath, FtpPath) Then
                            If CreateCompleteFile(CompleteFilePath) Then
                                'Set Status isuploaded set 0 to 1
                                Database.ExecuteSQL("Update videos SET isuploaded=1 WHERE videoid=" & intVideoID, Nothing, conFTPConnection)
                                WriteToAppLog("Video ID : " & Me.intVideoID & " Video Uploaded Successfully on FTP.")
                            Else
                                Throw New Exception("CreateCompleteFile function failed to upload complete file")
                            End If
                        Else
                            Throw New Exception("UploadVideoToFTP function failed to upload video")
                        End If
                    Else
                        Throw New Exception("File not found in videocache folder")
                    End If
                Else
                    Throw New Exception("Make Directory Function failed.")
                End If
            Else
                Throw New Exception("Unable to find User ID in videos")
            End If
        Catch ex As Exception
            WriteToAppLog("UploadVideoOnFTP Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
            WriteToErrorLog("UploadVideoOnFTP Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
            Call UpdateFTPError(ex.ToString)
        End Try
        If conFTPConnection IsNot Nothing Then conFTPConnection.Close() : conFTPConnection.Dispose()
    End Sub

    Private Function MakeDirectory(ByVal UserID As String) As Boolean
        Dim MakeDirectoryRequest As FtpWebRequest = Nothing
        Dim MakeDirectoryResponse As FtpWebResponse = Nothing
        Dim MakeDirectoryResponseStream As Stream = Nothing
        Dim intRetry As Integer = 0
        Dim blnSuccess As Boolean = False
        Do While intRetry < 3
            Try
                MakeDirectoryRequest = DirectCast(WebRequest.Create("ftp://" & FTPSetting.Host & "/Videos/"), FtpWebRequest)
                MakeDirectoryRequest.Method = WebRequestMethods.Ftp.ListDirectory
                MakeDirectoryRequest.KeepAlive = False
                ''request.Credentials = New NetworkCredential("convertit@propertytube.com", "QBJ&2FW*PtfV")
                MakeDirectoryRequest.Credentials = New NetworkCredential(FTPSetting.Username, FTPSetting.Password)
                MakeDirectoryResponse = DirectCast(MakeDirectoryRequest.GetResponse, FtpWebResponse)

                MakeDirectoryResponseStream = MakeDirectoryResponse.GetResponseStream
                Dim reader As New StreamReader(MakeDirectoryResponseStream)
                Dim CrLf As Char() = {Constants.vbCr, Constants.vbLf}
                Dim Direcotries As String = reader.ReadToEnd()

                Static UserIDs As String()
                If Not Direcotries.Length = 0 Then
                    UserIDs = Direcotries.Split(CrLf)
                End If
                reader.Close()
                MakeDirectoryResponse.Close()
                MakeDirectoryResponseStream.Close()

                If Direcotries.Length = 0 Then
                    MakeDirectoryRequest = DirectCast(WebRequest.Create("ftp://" & FTPSetting.Host & "/Videos/" & UserID.ToString), FtpWebRequest)
                    MakeDirectoryRequest.KeepAlive = False
                    MakeDirectoryRequest.Credentials = New NetworkCredential(FTPSetting.Username, FTPSetting.Password)
                    MakeDirectoryRequest.Method = WebRequestMethods.Ftp.MakeDirectory
                    MakeDirectoryResponse = DirectCast(MakeDirectoryRequest.GetResponse, FtpWebResponse)
                    MakeDirectoryResponseStream = MakeDirectoryResponse.GetResponseStream
                    'reader = New StreamReader(ResponseStream)
                    'Dim str As String = reader.ReadToEnd()
                    MakeDirectoryResponseStream.Close()
                    MakeDirectoryResponse.Close()
                    WriteToAppLog("MakeDirectory Function : UserID - " & UserID & " Directory Created Successfully")
                Else
                    If Not UserIDs.Contains(UserID) Then
                        MakeDirectoryRequest = DirectCast(WebRequest.Create("ftp://" & FTPSetting.Host & "/Videos/" & UserID.ToString), FtpWebRequest)
                        MakeDirectoryRequest.KeepAlive = False
                        MakeDirectoryRequest.Credentials = New NetworkCredential(FTPSetting.Username, FTPSetting.Password)
                        MakeDirectoryRequest.Method = WebRequestMethods.Ftp.MakeDirectory
                        MakeDirectoryResponse = DirectCast(MakeDirectoryRequest.GetResponse, FtpWebResponse)
                        MakeDirectoryResponseStream = MakeDirectoryResponse.GetResponseStream
                        'reader = New StreamReader(ResponseStream)
                        'Dim str As String = reader.ReadToEnd()
                        MakeDirectoryResponseStream.Close()
                        MakeDirectoryResponse.Close()
                        Array.Clear(UserIDs, 0, UserIDs.Length)
                        WriteToAppLog("MakeDirectory Function : UserID - " & UserID & " Directory Created Successfully")
                    Else
                        WriteToAppLog("MakeDirectory Function : UserID - " & UserID & " Directory Already Exist")
                    End If
                End If
                
                blnSuccess = True
                Exit Do
            Catch ex As Exception
                blnSuccess = False
                WriteToAppLog("MakeDirectory Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
                WriteToErrorLog("MakeDirectory Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
            End Try
            intRetry += 1
            If Not MakeDirectoryResponseStream Is Nothing Then MakeDirectoryResponseStream.Close()
            If Not MakeDirectoryResponse Is Nothing Then MakeDirectoryResponse.Close()
        Loop
        If Not MakeDirectoryResponseStream Is Nothing Then MakeDirectoryResponseStream.Close()
        If Not MakeDirectoryResponseStream Is Nothing Then MakeDirectoryResponseStream.Dispose()
        If Not MakeDirectoryRequest Is Nothing Then MakeDirectoryRequest.Abort()
        If Not MakeDirectoryResponse Is Nothing Then MakeDirectoryResponse.Close()
        Return blnSuccess
    End Function

    Private Function UploadVideoToFTP(ByVal source As String, ByVal target As String) As Boolean
        Dim request As FtpWebRequest = Nothing
        Dim reader As FileStream = Nothing
        Dim stream As Stream = Nothing
        Dim intRetry As Integer = 0
        Dim blnSuccess As Boolean = False
        Do While intRetry < 3
            Try
                request = DirectCast(WebRequest.Create(target), FtpWebRequest)
                request.KeepAlive = False
                request.Method = WebRequestMethods.Ftp.UploadFile
                request.Credentials = New NetworkCredential(FTPSetting.Username, FTPSetting.Password)

                reader = New FileStream(source, FileMode.Open)
                Dim data(4096) As Byte
                'Dim buffer(Convert.ToInt32(reader.Length - 1)) As Byte
                request.ContentLength = reader.Length - 1

                stream = request.GetRequestStream
                Dim intReadBytes As Integer
                Dim intOffSet As Int64 = 0
                Do
                    intReadBytes = reader.Read(data, 0, data.Length)
                    intOffSet += intReadBytes
                    stream.Write(data, 0, intReadBytes)

                    'Dim intProgress As Int64 = ((intOffSet * 100) / buffer.Length)
                    'DataRow.Cells("status").Value = intProgress
                Loop While intReadBytes > 0
                reader.Close() : reader.Dispose() : stream.Close() : stream.Dispose()
                request.Abort()
                WriteToAppLog("UploadVideoToFTP Function : VideoID - " & intVideoID & " Uploaded Successfully on FTP")
                blnSuccess = True
                Exit Do
            Catch ex As Exception
                blnSuccess = False
                WriteToAppLog("UploadVideoToFTP Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
                WriteToErrorLog("UploadVideoToFTP Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
            End Try
            intRetry += 1

            If Not reader Is Nothing Then reader.Close()
            If Not stream Is Nothing Then stream.Close()
        Loop
        If Not reader Is Nothing Then reader.Close() : reader.Dispose()
        If Not stream Is Nothing Then stream.Close() : stream.Dispose()
        If Not request Is Nothing Then request.Abort()
        Return blnSuccess
    End Function

    Private Function CreateCompleteFile(ByVal Target As String) As Boolean
        Dim request1 As FtpWebRequest = Nothing
        Dim stream1 As Stream = Nothing
        Dim blnSuccess As Boolean = False
        Dim intRetry As Integer = 0
        Do While intRetry < 3
            Try
                request1 = DirectCast(WebRequest.Create(Target), FtpWebRequest)
                request1.KeepAlive = False
                request1.Method = WebRequestMethods.Ftp.UploadFile
                request1.Credentials = New NetworkCredential(FTPSetting.Username, FTPSetting.Password)

                stream1 = request1.GetRequestStream
                stream1.Close() : stream1.Dispose()
                request1.Abort()
                WriteToAppLog("Video ID : " & Me.intVideoID & " - Complete file uploaded successfully")
                blnSuccess = True
                Exit Do
            Catch ex As Exception
                blnSuccess = False
                WriteToAppLog("CreateCompleteFile Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
                WriteToErrorLog("CreateCompleteFile Function : VideoID - " & intVideoID & " Error : " & ex.ToString)
            End Try
            intRetry += 1
            If Not stream1 Is Nothing Then stream1.Close()
        Loop
        If Not stream1 Is Nothing Then stream1.Close() : stream1.Dispose()
        If Not request1 Is Nothing Then request1.Abort()
        Return blnSuccess
    End Function

    Private Sub UpdateFTPError(ByVal strerror As String)
        Dim conFTPError As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conFTPError = Database.Connection.Clone
            If Not conFTPError.State = ConnectionState.Open Then Call conFTPError.Open()

            Dim cmdFTPError As New MySqlCommand("UPDATE videos SET uploaderror=?uploaderror, isuploaded=?isuploaded WHERE videoid=?videoid", conFTPError)
            With cmdFTPError
                With .Parameters
                    Call .AddWithValue("uploaderror", strerror)
                    Call .AddWithValue("isuploaded", -1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
        Catch ex As Exception
            WriteToErrorLog("UpdateFTPError Function : " & ex.ToString)
            WriteToAppLog("UpdateFTPError Function : " & ex.ToString)
        End Try
        If conFTPError IsNot Nothing Then conFTPError.Close() : conFTPError.Dispose()
    End Sub

    Private Sub ConvertVideoForWebm()
        Dim WebmProcess As New Process
        Dim conWebm As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conWebm = Database.Connection.Clone
            If Not conWebm.State = ConnectionState.Open Then Call conWebm.Open()

            Dim strInputFilePath As String = ""
            strInputFilePath = Common.ConvertNull(Database.ExecuteSQL("select videopath from videos where videoid=" & intVideoID, Nothing, conWebm), "")
            If strInputFilePath = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Video Filepath not found in database")
                Throw New Exception("Error : Video Filepath not found in database")
            End If

            Dim VideoFileName As String = ""
            'VideoFileName = Database.ExecuteSQL("select sdwebmvideoname from videos where videoid=" & intVideoID, Nothing, conWebm)
            'If VideoFileName = "" Then
            '    WriteToAppLog("VideoID : " & intVideoID & " - Webm Video Filename not found in database")
            '    Throw New Exception("Error : Webm Video Filename not found in database")
            'End If

            VideoFileName = "final_webm"
            Dim strVideoCachePath As String = strInputFilePath.Substring(0, strInputFilePath.Length - 9)
            Dim strOutputFilePath As String = strVideoCachePath & VideoFileName & ".webm"

            If File.Exists(strOutputFilePath) Then
                File.Delete(strOutputFilePath)
            End If

            Dim result As String = ""
            Dim errorreader As StreamReader = Nothing

            WebmProcess.StartInfo.UseShellExecute = False
            WebmProcess.StartInfo.ErrorDialog = False
            WebmProcess.StartInfo.RedirectStandardError = True
            WebmProcess.StartInfo.CreateNoWindow = True
            WebmProcess.StartInfo.FileName = """" & Application.StartupPath & "\libs\ffmpeg_new\ffmpeg.exe" & """"
            WebmProcess.StartInfo.Arguments = " -y -i " & """" & strInputFilePath & """" & " -b:v 1500k " & """" & strOutputFilePath & """"
            WebmProcess.Start()
            errorreader = WebmProcess.StandardError
            result = errorreader.ReadToEnd()
            WebmProcess.WaitForExit()

            Dim WebmVideoLength As Integer = 0
            If File.Exists(strOutputFilePath) Then
                WebmVideoLength = Common.GetFileSize(strOutputFilePath)
            Else
                Throw New Exception("Video ID : " & intVideoID & " Webm Video Converting failed.")
            End If

            Dim cmdWebm As New MySqlCommand("update videos set iswebmvideo=?iswebmvideo, webmvideolength=?webmvideolength where videoid=?videoid", conWebm)
            With cmdWebm
                With .Parameters
                    Call .AddWithValue("iswebmvideo", 1)
                    Call .AddWithValue("webmvideolength", WebmVideoLength)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToAppLog("VideoID : " & intVideoID & " - Video Sucessfully converted into Webm format...!!!")

        Catch ex As Exception
            Dim cmdWebmError As New MySqlCommand("update videos set iswebmvideo=?iswebmvideo where videoid=?videoid", conWebm)
            With cmdWebmError
                With .Parameters
                    Call .AddWithValue("iswebmvideo", -1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToErrorLog("CovertVideoForWebm Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
        End Try
        If WebmProcess IsNot Nothing Then WebmProcess.Close() : WebmProcess.Dispose()
        If conWebm IsNot Nothing Then conWebm.Close() : conWebm.Dispose()
    End Sub

    Private Sub ConvertVideoForMobile()
        Dim SmartphoneProcess As New Process
        Dim conSmartPhone As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conSmartPhone = Database.Connection.Clone
            If Not conSmartPhone.State = ConnectionState.Open Then Call conSmartPhone.Open()

            Dim strInputFilePath As String = ""
            strInputFilePath = Common.ConvertNull(Database.ExecuteSQL("select videopath from videos where videoid=" & intVideoID, Nothing, conSmartPhone), "")

            If strInputFilePath = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Video Filepath not found in database")
                Throw New Exception("Error : Video Filepath not found in database")
            End If

            Dim VideoFileName As String = ""
            'VideoFileName = Database.ExecuteSQL("select smartphonevideoname from videos where videoid=" & intVideoID, Nothing, conSmartPhone)
            'If VideoFileName = "" Then
            '    WriteToAppLog("VideoID : " & intVideoID & " - Smartphone Video Filename not found in database")
            '    Throw New Exception("Error : Smartphone Video Filename not found in database")
            'End If

            VideoFileName = "final_smartphone"
            Dim strVideoCachePath As String = strInputFilePath.Substring(0, strInputFilePath.Length - 9)
            Dim strOutputFilePath As String = strVideoCachePath & VideoFileName & ".mp4"

            If File.Exists(strOutputFilePath) Then
                File.Delete(strOutputFilePath)
            End If

            Dim result As String = ""
            Dim errorreader As StreamReader = Nothing

            SmartphoneProcess.StartInfo.UseShellExecute = False
            SmartphoneProcess.StartInfo.ErrorDialog = False
            SmartphoneProcess.StartInfo.RedirectStandardError = True
            SmartphoneProcess.StartInfo.CreateNoWindow = True
            SmartphoneProcess.StartInfo.FileName = """" & Application.StartupPath & "\libs\ffmpeg_new\ffmpeg.exe" & """"
            If blnMakeHDVideo Then
                'smartphone video in 320p dimention for ZA client for Non ZA Client
                SmartphoneProcess.StartInfo.Arguments = " -y -i " & """" & strInputFilePath & """" & " -s 480x320 " & """" & strOutputFilePath & """"
            Else
                'smartphone video in 240p dimention for ZA client
                SmartphoneProcess.StartInfo.Arguments = " -y -i " & """" & strInputFilePath & """" & " -s 426x240 " & """" & strOutputFilePath & """"
            End If
            SmartphoneProcess.Start()
            errorreader = SmartphoneProcess.StandardError
            result = errorreader.ReadToEnd()
            SmartphoneProcess.WaitForExit()

            Dim SmartphoneVideoLength As Integer = 0
            If File.Exists(strOutputFilePath) Then
                SmartphoneVideoLength = Common.GetFileSize(strOutputFilePath)
            Else
                Throw New Exception("Video ID : " & intVideoID & " Smartphone Video Converting failed.")
            End If

            Dim cmdSmartphone As New MySqlCommand("update videos set issmartphonevideo=?issmartphonevideo, smartphonevideolength=?smartphonevideolength where videoid=?videoid", conSmartPhone)
            With cmdSmartphone
                With .Parameters
                    Call .AddWithValue("issmartphonevideo", 1)
                    Call .AddWithValue("smartphonevideolength", SmartphoneVideoLength)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With

            WriteToAppLog("VideoID : " & intVideoID & " - Smartphone Video Sucessfully converted into mp4 format...!!!")

        Catch ex As Exception

            Dim cmdSmartphoneError As New MySqlCommand("update videos set issmartphonevideo=?issmartphonevideo where videoid=?videoid", conSmartPhone)
            With cmdSmartphoneError
                With .Parameters
                    Call .AddWithValue("issmartphonevideo", -1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToErrorLog("ConvertVideoForMobile Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
        End Try
        If SmartphoneProcess IsNot Nothing Then SmartphoneProcess.Close() : SmartphoneProcess.Dispose()
        If conSmartPhone IsNot Nothing Then conSmartPhone.Close() : conSmartPhone.Dispose()
    End Sub

    Private Sub UploadMP4VideoOnAmazone()
        Dim conMP4Amazone As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conMP4Amazone = Database.Connection.Clone
            If Not conMP4Amazone.State = ConnectionState.Open Then Call conMP4Amazone.Open()

            Dim strInputFilePath As String = ""
            strInputFilePath = Common.ConvertNull(Database.ExecuteSQL("select videopath from videos where videoid=" & intVideoID, Nothing, conMP4Amazone), "")
            If strInputFilePath = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Video Filepath not found in database")
                Throw New Exception("Error : Video Filepath not found in database")
            End If

            Dim VideoFileName As String = ""
            VideoFileName = Database.ExecuteSQL("select videoname from videos where videoid=" & intVideoID, Nothing, conMP4Amazone)
            If VideoFileName = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Video Filename not found in database")
                Throw New Exception("Error : Video Filename not found in database")
            End If

            If File.Exists(strInputFilePath) Then
                If Not blnMakeHDVideo Then
                    VideoFileName = VideoFileName & "_360"
                End If
                Dim blnResult As Boolean = UploadFileOnAmazon(strInputFilePath, VideoFileName & ".mp4")
                If blnResult Then
                    Dim cmdMp4 As New MySqlCommand("update videos set ismp4videouploaded=?ismp4videouploaded where videoid=?videoid", conMP4Amazone)
                    With cmdMp4
                        With .Parameters
                            Call .AddWithValue("ismp4videouploaded", 1)
                            Call .AddWithValue("videoid", intVideoID)
                        End With
                        Call .ExecuteNonQuery()
                    End With
                    WriteToAppLog("VideoID : " & intVideoID & " - Mp4 file Successfully Uploaded On Amazon..!!!")
                Else
                    WriteToAppLog("Error - Mp4 file Uploading failed On Amazon.")
                    Throw New Exception("Error - Mp4 file Uploading failed On Amazon.")
                End If
            Else
                WriteToAppLog("Error - Mp4 file Uploading failed On Amazon beacause Source file not exist")
                Throw New Exception("Error - Mp4 file Uploading failed On Amazon beacause Source file not exist")
            End If
        Catch ex As Exception
            Dim cmdMp4Error As New MySqlCommand("update videos set ismp4videouploaded=?ismp4videouploaded where videoid=?videoid", conMP4Amazone)
            With cmdMp4Error
                With .Parameters
                    Call .AddWithValue("ismp4videouploaded", -1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToErrorLog("UploadMP4VideoOnAmazone Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
        End Try
        If conMP4Amazone IsNot Nothing Then conMP4Amazone.Close() : conMP4Amazone.Dispose()
    End Sub

    Private Sub UploadWebmVideoOnAmazone()
        Dim conWebmAmazone As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conWebmAmazone = Database.Connection.Clone
            If Not conWebmAmazone.State = ConnectionState.Open Then Call conWebmAmazone.Open()

            Dim strInputFilePath As String = ""
            strInputFilePath = Common.ConvertNull(Database.ExecuteSQL("select videopath from videos where videoid=" & intVideoID, Nothing, conWebmAmazone), "")
            If strInputFilePath = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Video Filepath not found in database")
                Throw New Exception("Error : Video Filepath not found in database")
            End If

            Dim VideoFileName As String = ""
            If blnMakeHDVideo Then
                VideoFileName = Database.ExecuteSQL("select sdwebmvideoname from videos where videoid=" & intVideoID, Nothing, conWebmAmazone)
            Else
                VideoFileName = Database.ExecuteSQL("select videoname from videos where videoid=" & intVideoID, Nothing, conWebmAmazone)
            End If
            If VideoFileName = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Webm Video Filename not found in database")
                Throw New Exception("Error : Webm Video Filename not found in database")
            End If

            Dim strVideoCachePath As String = strInputFilePath.Substring(0, strInputFilePath.Length - 9)
            Dim strSourceFilePath As String = strVideoCachePath & "final_webm.webm"
            If File.Exists(strSourceFilePath) Then
                If Not blnMakeHDVideo Then
                    VideoFileName = VideoFileName & "_360"
                End If
                Dim blnResult As Boolean = UploadFileOnAmazon(strSourceFilePath, VideoFileName & ".webm")
                If blnResult Then
                    Dim cmdWebm As New MySqlCommand("update videos set iswebmvideouploaded=?iswebmvideouploaded where videoid=?videoid", conWebmAmazone)
                    With cmdWebm
                        With .Parameters
                            Call .AddWithValue("iswebmvideouploaded", 1)
                            Call .AddWithValue("videoid", intVideoID)
                        End With
                        Call .ExecuteNonQuery()
                    End With
                    WriteToAppLog("VideoID : " & intVideoID & " - Webm file Successfully Uploaded On Amazon..!!!")
                Else
                    WriteToAppLog("Error - Webm file Uploading failed On Amazon.")
                    Throw New Exception("Error - Webm file Uploading failed On Amazon.")
                End If
            Else
                WriteToAppLog("Error - Webm file Uploading failed On Amazon beacause Source file not exist")
                Throw New Exception("Error - Webm file Uploading failed On Amazon beacause Source file not exist")
            End If
        Catch ex As Exception
            Dim cmdWebmError As New MySqlCommand("update videos set iswebmvideouploaded=?iswebmvideouploaded where videoid=?videoid", conWebmAmazone)
            With cmdWebmError
                With .Parameters
                    Call .AddWithValue("iswebmvideouploaded", -1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToErrorLog("UploadWebmVideoOnAmazone Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
        End Try
        If conWebmAmazone IsNot Nothing Then conWebmAmazone.Close() : conWebmAmazone.Dispose()
    End Sub

    Private Sub UploadSmartphoneVideoOnAmazone()
        Dim conSmartPhoneAmazone As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conSmartPhoneAmazone = Database.Connection.Clone
            If Not conSmartPhoneAmazone.State = ConnectionState.Open Then Call conSmartPhoneAmazone.Open()

            Dim strInputFilePath As String = ""
            strInputFilePath = Common.ConvertNull(Database.ExecuteSQL("select videopath from videos where videoid=" & intVideoID, Nothing, conSmartPhoneAmazone), "")
            If strInputFilePath = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Video Filepath not found in database")
                Throw New Exception("Error : Video Filepath not found in database")
            End If

            Dim VideoFileName As String = ""
            If blnMakeHDVideo Then
                VideoFileName = Database.ExecuteSQL("select smartphonevideoname from videos where videoid=" & intVideoID, Nothing, conSmartPhoneAmazone)
            Else
                VideoFileName = Database.ExecuteSQL("select videoname from videos where videoid=" & intVideoID, Nothing, conSmartPhoneAmazone)
            End If

            If VideoFileName = "" Then
                WriteToAppLog("VideoID : " & intVideoID & " - Smartphone Video Filename not found in database")
                Throw New Exception("Error : Smartphone Video Filename not found in database")
            End If

            Dim strVideoCachePath As String = strInputFilePath.Substring(0, strInputFilePath.Length - 9)
            Dim strSourceFilePath As String = strVideoCachePath & "final_smartphone.mp4"

            If File.Exists(strSourceFilePath) Then
                If Not blnMakeHDVideo Then
                    VideoFileName = VideoFileName & "_240"
                End If
                Dim blnResult As Boolean = UploadFileOnAmazon(strSourceFilePath, VideoFileName & ".mp4")
                If blnResult Then
                    Dim cmdSmartphone As New MySqlCommand("update videos set issmartphonevideouploaded=?issmartphonevideouploaded where videoid=?videoid", conSmartPhoneAmazone)
                    With cmdSmartphone
                        With .Parameters
                            Call .AddWithValue("issmartphonevideouploaded", 1)
                            Call .AddWithValue("videoid", intVideoID)
                        End With
                        Call .ExecuteNonQuery()
                    End With
                    WriteToAppLog("VideoID : " & intVideoID & " - Samrtphone Video file Successfully Uploaded On Amazon..!!!")
                Else
                    WriteToAppLog("Error - Samrtphone Video file Uploading failed On Amazon.")
                    Throw New Exception("Error - Samrtphone Video file Uploading failed On Amazon.")
                End If
            Else
                WriteToAppLog("Error - Smartphone video file Uploading failed On Amazon beacause Source file not exist")
                Throw New Exception("Error - Smartphone video file Uploading failed On Amazon beacause Source file not exist")
            End If

        Catch ex As Exception
            Dim cmdSmartphoneError As New MySqlCommand("update videos set issmartphonevideouploaded=?issmartphonevideouploaded where videoid=?videoid", conSmartPhoneAmazone)
            With cmdSmartphoneError
                With .Parameters
                    Call .AddWithValue("issmartphonevideouploaded", -1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToErrorLog("UploadSmartphoneVideoOnAmazone Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
        End Try
        If conSmartPhoneAmazone IsNot Nothing Then conSmartPhoneAmazone.Close() : conSmartPhoneAmazone.Dispose()
    End Sub

    Private Sub UploadMp4ThumbnailOnAmazone()
        Dim conMp4Thumb As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conMp4Thumb = Database.Connection.Clone
            If Not conMp4Thumb.State = ConnectionState.Open Then Call conMp4Thumb.Open()

            If CreateThumbnailOriginalImage() Then
                Dim dtMp4ThumbInfo As DataTable = Database.FetchDataTable("select mp4thumbnail from videos where videoid=" & intVideoID, Nothing, conMp4Thumb)
                If Not dtMp4ThumbInfo Is Nothing Then
                    If dtMp4ThumbInfo.Rows.Count > 0 Then
                        If Not dtMp4ThumbInfo.Rows(0).Item("mp4thumbnail") = "" Then
                            If ResizeThumbnaiImage(640, 360, dtMp4ThumbInfo.Rows(0).Item("mp4thumbnail")) Then '  If ResizeThumbnaiImage(600, 450, dtMp4ThumbInfo.Rows(0).Item("mp4thumbnail")) Then
                                Dim FileName As String = dtMp4ThumbInfo.Rows(0).Item("mp4thumbnail") & ".png"
                                Dim SourceFileName As String = Application.StartupPath & "/Thumbnails/" & intVideoID & "/" & FileName
                                If UploadFileOnAmazon(SourceFileName, FileName) Then
                                    Dim cmdMp4Thumb As New MySqlCommand("update videos set isuploadmp4thumb=?isuploadmp4thumb where videoid=?videoid", conMp4Thumb)
                                    With cmdMp4Thumb
                                        With .Parameters
                                            Call .AddWithValue("isuploadmp4thumb", 1)
                                            Call .AddWithValue("videoid", intVideoID)
                                        End With
                                        Call .ExecuteNonQuery()
                                    End With
                                    WriteToAppLog("Video ID : " & intVideoID & " - Mp4 Thumbnail file Successfully Uploaded On Amazon..!!!")
                                Else
                                    WriteToAppLog("Video ID : " & intVideoID & " : Error - Mp4 Thumbnail file Uploading failed On Amazon.")
                                    Throw New Exception("Video ID : " & intVideoID & " : Error - Mp4 Thumbnail file Uploading failed On Amazon.")
                                End If
                            Else
                                WriteToAppLog("Video ID : " & intVideoID & " : Error - Mp4 Thumbnail Resizing Failed")
                                Throw New Exception("Video ID : " & intVideoID & " : Error - Mp4 Thumbnail Resizing Failed")
                            End If
                        Else
                            WriteToAppLog("Video ID : " & intVideoID & " : Error - UnDefine Mp4 Thumbnail Filename")
                            Throw New Exception("Video ID : " & intVideoID & " : Error - UnDefine Mp4 Thumbnail Filename")
                        End If
                    Else
                        WriteToAppLog("Video ID : " & intVideoID & " : Error - UnDefine Mp4 Thumbnail Filename")
                        Throw New Exception("Video ID : " & intVideoID & " : Error - UnDefine Mp4 Thumbnail Filename")
                    End If
                Else
                    WriteToAppLog("Video ID : " & intVideoID & " : Error - UnDefine Mp4 Thumbnail Filename")
                    Throw New Exception("Video ID : " & intVideoID & " : Error - UnDefine Mp4 Thumbnail Filename")
                End If
            Else
                WriteToAppLog("Video ID : " & intVideoID & " : Error - CreateThumbnailOriginalImage Function Failed.")
                Throw New Exception("Video ID : " & intVideoID & " : Error - CreateThumbnailOriginalImage Function Failed.")
            End If
        Catch ex As Exception
            Dim cmdMp4ThumbError As New MySqlCommand("update videos set isuploadmp4thumb=?isuploadmp4thumb where videoid=?videoid", conMp4Thumb)
            With cmdMp4ThumbError
                With .Parameters
                    Call .AddWithValue("isuploadmp4thumb", -1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToErrorLog("UploadMp4Thumbnail Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
        End Try
        If conMp4Thumb IsNot Nothing Then conMp4Thumb.Close() : conMp4Thumb.Dispose()
    End Sub

    Private Sub UploadSmartphoneThumbnailOnAmazone()
        Dim conSmartphoneThumb As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conSmartphoneThumb = Database.Connection.Clone
            If Not conSmartphoneThumb.State = ConnectionState.Open Then Call conSmartphoneThumb.Open()

            If CreateThumbnailOriginalImage() Then
                Dim dtSmartphoneThumbInfo As DataTable = Database.FetchDataTable("select smartphonethumbnail from videos where videoid=" & intVideoID, Nothing, conSmartphoneThumb)
                If Not dtSmartphoneThumbInfo Is Nothing Then
                    If dtSmartphoneThumbInfo.Rows.Count > 0 Then
                        If Not dtSmartphoneThumbInfo.Rows(0).Item("smartphonethumbnail") = "" Then
                            If ResizeThumbnaiImage(480, 320, dtSmartphoneThumbInfo.Rows(0).Item("smartphonethumbnail")) Then
                                Dim FileName As String = dtSmartphoneThumbInfo.Rows(0).Item("smartphonethumbnail") & ".png"
                                Dim SourceFileName As String = Application.StartupPath & "/Thumbnails/" & intVideoID & "/" & FileName
                                If UploadFileOnAmazon(SourceFileName, FileName) Then
                                    Dim cmdMp4Thumb As New MySqlCommand("update videos set isuploadsmartphonethumb=?isuploadsmartphonethumb where videoid=?videoid", conSmartphoneThumb)
                                    With cmdMp4Thumb
                                        With .Parameters
                                            Call .AddWithValue("isuploadsmartphonethumb", 1)
                                            Call .AddWithValue("videoid", intVideoID)
                                        End With
                                        Call .ExecuteNonQuery()
                                    End With
                                    WriteToAppLog("Video ID : " & intVideoID & " - SmartPhone Thumbnail file Successfully Uploaded On Amazon..!!!")
                                Else
                                    WriteToAppLog("Video ID : " & intVideoID & " : Error - SmartPhone Thumbnail file Uploading failed On Amazon.")
                                    Throw New Exception("Video ID : " & intVideoID & " : Error - SmartPhone Thumbnail file Uploading failed On Amazon.")
                                End If
                            Else
                                WriteToAppLog("Video ID : " & intVideoID & " : Error - SmartPhone Thumbnail Resizing Failed")
                                Throw New Exception("Video ID : " & intVideoID & " : Error - SmartPhone Thumbnail Resizing Failed")
                            End If
                        Else
                            WriteToAppLog("Video ID : " & intVideoID & " : Error - UnDefine SmartPhone Thumbnail Filename")
                            Throw New Exception("Video ID : " & intVideoID & " : Error - UnDefine SmartPhone Thumbnail Filename")
                        End If
                    Else
                        WriteToAppLog("Video ID : " & intVideoID & " : Error - UnDefine SmartPhone Thumbnail Filename")
                        Throw New Exception("Video ID : " & intVideoID & " : Error - UnDefine SmartPhone Thumbnail Filename")
                    End If
                Else
                    WriteToAppLog("Video ID : " & intVideoID & " : Error - UnDefine SmartPhone Thumbnail Filename")
                    Throw New Exception("Video ID : " & intVideoID & " : Error - UnDefine SmartPhone Thumbnail Filename")
                End If
            Else
                WriteToAppLog("Video ID : " & intVideoID & " : Error - CreateThumbnailOriginalImage Function Failed.")
                Throw New Exception("Video ID : " & intVideoID & " : Error - CreateThumbnailOriginalImage Function Failed.")
            End If
        Catch ex As Exception
            Dim cmdMp4ThumbError As New MySqlCommand("update videos set isuploadsmartphonethumb=?isuploadsmartphonethumb where videoid=?videoid", conSmartphoneThumb)
            With cmdMp4ThumbError
                With .Parameters
                    Call .AddWithValue("isuploadsmartphonethumb", 1)
                    Call .AddWithValue("videoid", intVideoID)
                End With
                Call .ExecuteNonQuery()
            End With
            WriteToErrorLog("UploadSmartphoneThumbnail Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
        End Try
        If conSmartphoneThumb IsNot Nothing Then conSmartphoneThumb.Close() : conSmartphoneThumb.Dispose()
    End Sub

    Private Function ResizeThumbnaiImage(ByVal ThumbWidth As Integer, ByVal ThumbHeight As Integer, ByVal OutputFileName As String) As Boolean
        Try
            If File.Exists(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Origional.jpg") Then
                Dim OrigionalImage As Image
                OrigionalImage = Image.FromFile(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Origional.jpg")

                Dim CropX As Integer = 0
                Dim CropY As Integer = 0
                Dim ResizeX As Integer = 0
                Dim ResizeY As Integer = 0

                Dim AspectWidth As Integer = 0
                Dim AspectHeight As Integer = 0
                ' If images.Width > ThumbWidth And images.Height > ThumbHeight Then

                If OrigionalImage.Width > OrigionalImage.Height Then
                    AspectHeight = OrigionalImage.Height
                    AspectWidth = (OrigionalImage.Height * ThumbWidth) / ThumbHeight
                Else
                    AspectWidth = OrigionalImage.Width
                    AspectHeight = (OrigionalImage.Width * ThumbHeight) / ThumbWidth
                End If

                Dim bmp As New Bitmap(AspectWidth, AspectHeight)
                Dim objGrpahics As Graphics = Graphics.FromImage(bmp)
                objGrpahics.DrawImage(OrigionalImage, New Rectangle(0, 0, AspectWidth, AspectHeight))
                bmp.Save(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Test.jpg", Imaging.ImageFormat.Jpeg)
                bmp.Dispose()
                objGrpahics.Dispose()
                OrigionalImage.Dispose()

                Dim finalImage As Image
                finalImage = Image.FromFile(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Test.jpg")

                If finalImage.Width > ThumbWidth Then
                    CropX = (finalImage.Width - ThumbWidth) / 2
                Else
                    ResizeX = (ThumbWidth - finalImage.Width) / 2
                End If

                If finalImage.Height > ThumbHeight Then
                    CropY = (finalImage.Height - ThumbHeight) / 2
                Else
                    ResizeY = (ThumbHeight - finalImage.Height) / 2
                End If

                Dim CropRect As New Rectangle(CropX, CropY, ThumbWidth, ThumbHeight)
                Dim CropImage As New Bitmap(CropRect.Width, CropRect.Height)
                Dim grpResized As Graphics = Graphics.FromImage(CropImage)
                grpResized.DrawImage(finalImage, New Rectangle(ResizeX, ResizeY, CropRect.Width, CropRect.Height), CropRect, GraphicsUnit.Pixel)
                finalImage.Dispose()
                CropImage.Save(Application.StartupPath & "/Thumbnails/" & intVideoID & "/" & OutputFileName & ".png", Imaging.ImageFormat.Png)
                CropImage.Dispose()
                grpResized.Dispose()

                If File.Exists(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Test.jpg") Then
                    File.Delete(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Test.jpg")
                End If
                ResizeThumbnaiImage = True
                WriteToAppLog("Video ID : " & intVideoID & " Thumbnail File Name" & OutputFileName & ".png Resized Successfully")
            Else
                WriteToAppLog("Video ID :" & intVideoID & " Origional.jpg File Not found. Resizing Failed...")
                Throw New Exception("Video ID :" & intVideoID & " Origional.jpg File Not found. Resizing Failed...")
            End If
        Catch ex As Exception
            WriteToErrorLog("ResizeThumbnaiImage Procedures Video ID : " & intVideoID & " : Error -" & ex.ToString)
            ResizeThumbnaiImage = False
        End Try
    End Function

    Private Function CreateThumbnailOriginalImage() As Boolean
        Try
            If Not Directory.Exists(Application.StartupPath & "/Thumbnails") Then
                Directory.CreateDirectory(Application.StartupPath & "/Thumbnails")
            End If
            If Not Directory.Exists(Application.StartupPath & "/Thumbnails/" & intVideoID) Then
                Directory.CreateDirectory(Application.StartupPath & "/Thumbnails/" & intVideoID)
            End If
            If Not File.Exists(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Origional.jpg") Then
                Dim ThumbnaiOrigionalImageName As String = ""
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

                Dim intFirstImage As Integer = 1

                For Each Fname As Integer In FKey
                    Dim images As Image = Nothing
                    Try
                        images = Image.FromFile(IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg") 'Image.FromFile(strImageLocalPath & "\" & intVideoId & "\" & i & ".jpg")
                    Catch ex As Exception
                        WriteToAppLog("Image Corrupted - Image Path - " & IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg")
                        WriteToErrorLog("Image Corrupted - Image Path - " & IMAGE_LOCAL_PATH & "\" & intVideoID & "\" & Fname.ToString & ".jpg")
                    End Try


                    If Not images Is Nothing Then
                        If intFirstImage = 1 Then
                            ThumbnaiOrigionalImageName = Fname.ToString
                            images.Dispose()
                            Exit For
                        End If
                        intFirstImage += 1
                    End If
                Next

                If File.Exists(IMAGE_LOCAL_PATH & intVideoID & "/" & ThumbnaiOrigionalImageName & ".jpg") Then
                    Dim ThumbImage As Image = Nothing

                    Try
                        ThumbImage = Image.FromFile(IMAGE_LOCAL_PATH & intVideoID & "/" & ThumbnaiOrigionalImageName & ".jpg")
                        Dim thumbBitmap As New Bitmap(ThumbImage.Width, ThumbImage.Height)
                        Dim objGrpahics As Graphics = Graphics.FromImage(thumbBitmap)
                        objGrpahics.DrawImage(ThumbImage, New Rectangle(0, 0, ThumbImage.Width, ThumbImage.Height))
                        thumbBitmap.Save(Application.StartupPath & "/Thumbnails/" & intVideoID & "/Origional.jpg", Imaging.ImageFormat.Jpeg)
                        thumbBitmap.Dispose()
                        ThumbImage.Dispose()
                        CreateThumbnailOriginalImage = True
                        WriteToAppLog("Video ID : " & intVideoID & "Thumbnai Original image Created successfully")

                        If Directory.Exists(IMAGE_LOCAL_PATH & intVideoID) Then
                            Directory.Delete(IMAGE_LOCAL_PATH & intVideoID, True)
                        End If

                    Catch ex As Exception
                        Throw New Exception("Video ID : " & intVideoID & " Thumbnail Image Corrupted - Error :" & ex.ToString)
                    End Try
                Else
                    CreateThumbnailOriginalImage = False
                    WriteToAppLog("VideoID - " & intVideoID & " : Thumbnail First Image File Not Found in Image Folder")
                    WriteToErrorLog("VideoID - " & intVideoID & " : Thumbnail First Image File Not Found in Image Folder")
                End If
            Else
                CreateThumbnailOriginalImage = True
                WriteToAppLog("VideoID - " & intVideoID & " : Thumbnail Origional Image Already Exist.")
            End If


        Catch ex As Exception
            WriteToAppLog("CreateThumbnailOriginalImage Function : " & ex.ToString)
            WriteToErrorLog("CreateThumbnailOriginalImage Function Video ID : " & intVideoID & " : Error -" & ex.ToString)
            CreateThumbnailOriginalImage = False
        End Try
    End Function

    Private Function UploadFileOnAmazon(ByVal Source As String, ByVal FileName As String) As Boolean
        Dim conAmazon As MySqlConnection = Nothing
        Try
            If Not Database.Connection.State = ConnectionState.Open Then Call Database.OpenConnection()
            conAmazon = Database.Connection.Clone
            If Not conAmazon.State = ConnectionState.Open Then Call conAmazon.Open()

            'Dim BucketName As String = Database.ExecuteSQL("select bucket from videos where videoid=" & intVideoId, Nothing, conAmazon)
            'Dim FolderName As String = Database.ExecuteSQL("select foldername from videos where videoid=" & intVideoId, Nothing, conAmazon)
            Dim dtBucketinfo As DataTable = Database.FetchDataTable("select bucket, foldername from videos where videoid=" & intVideoID, Nothing, conAmazon)
            If Not dtBucketinfo Is Nothing Then
                If dtBucketinfo.Rows.Count > 0 Then

                    Dim BucketName As String = ""
                    If Common.IsNull(dtBucketinfo.Rows(0).Item("bucket")) Then
                        BucketName = Common.ConvertNull(dtBucketinfo.Rows(0).Item("bucket"), "")
                    Else
                        BucketName = dtBucketinfo.Rows(0).Item("bucket")
                    End If

                    Dim FolderName As String = ""
                    If Common.IsNull(dtBucketinfo.Rows(0).Item("foldername")) Then
                        FolderName = Common.ConvertNull(dtBucketinfo.Rows(0).Item("foldername"), "")
                    Else
                        FolderName = dtBucketinfo.Rows(0).Item("foldername") & "/"
                    End If

                    If BucketName = "" Then
                        Throw New Exception("BucketName not found in Video ID - " & intVideoID)
                    End If
                    If FolderName = "" Then
                        Throw New Exception("FolderName not found in Video ID - " & intVideoID)
                    End If

                    Dim Client As AmazonS3
                    Client = New AmazonS3Client(AWS_ACCESS_KEY, AWS_SECRET_KEY)


                    Dim objListObjectRequest As ListObjectsRequest
                    objListObjectRequest = New ListObjectsRequest
                    objListObjectRequest.BucketName = BucketName
                    objListObjectRequest.Prefix = ""

                    Dim IsExistFolder As Boolean = False
                    Dim objResponse As New ListObjectsResponse
                    objResponse = Client.ListObjects(objListObjectRequest)

                    For Each objObject As S3Object In objResponse.S3Objects
                        If objObject.Key = FolderName Then
                            IsExistFolder = True
                            Exit For
                        End If
                    Next

                    If Not IsExistFolder Then
                        Dim request As New PutObjectRequest
                        request.BucketName = BucketName
                        request.Key = FolderName
                        request.ContentBody = ""
                        request.Timeout = 3600000 ' 1 hour
                        WriteToAppLog("Folder Created Successfully..!!!")
                    End If

                    Dim FileRequest As New PutObjectRequest
                    Dim FileRespose As New PutObjectResponse

                    FileRequest.BucketName = BucketName
                    FileRequest.Key = FolderName & FileName
                    FileRequest.WithFilePath(Source)
                    FileRequest.Timeout = 3600000 ' 1 hour
                    FileRespose = Client.PutObject(FileRequest)

                    If Not FileRespose Is Nothing Then
                        Dim cmdAmazone As New MySqlCommand("insert into amazoneresponse (videoid, filename, requestid, amazoneid2, creationdate) values(?videoid, ?filename, ?requestid, ?amazoneid2, ?creationdate)", conAmazon)
                        With cmdAmazone
                            With .Parameters
                                Call .AddWithValue("videoid", intVideoID)
                                Call .AddWithValue("filename", FileName)
                                Call .AddWithValue("requestid", FileRespose.RequestId)
                                Call .AddWithValue("amazoneid2", FileRespose.AmazonId2)
                                Call .AddWithValue("creationdate", Format(Now, "yyyy-MM-dd HH:mm:ss"))
                            End With
                            .ExecuteNonQuery()
                        End With
                        WriteToAppLog("Video ID - " & intVideoID & ", FileName - " & FileName & " Successfully Uploaded On Amazon..!!!")
                        UploadFileOnAmazon = True
                    Else
                        Throw New Exception("Error : Amazone File Response is nothing. Uploading Failed. Video ID - " & intVideoID)
                    End If
                Else
                    Throw New Exception("Error : Undefine Bucket info in Video ID - " & intVideoID)
                End If
            Else
                Throw New Exception("Error : Undefine Bucket info in Video ID - " & intVideoID)
            End If

        Catch ex As Exception
            UploadFileOnAmazon = False
            WriteToErrorLog("UploadVideoOnAmazon Function Video ID : " & intVideoID & " : Error -" & ex.ToString)
            WriteToAppLog("File Name : " & FileName & " - Error in UploadVideoOnAmazon Function : " & ex.ToString)
        End Try
        If conAmazon IsNot Nothing Then conAmazon.Close() : conAmazon.Dispose()
    End Function

End Class
