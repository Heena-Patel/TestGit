Imports System.ComponentModel
Imports System.Configuration.Install

Public Class ProjectInstaller

    Public Sub New()
        MyBase.New()

        'This call is required by the Component Designer.
        InitializeComponent()

        'Add initialization code after the call to InitializeComponent

    End Sub

    Private Sub ServiceProcessInstaller1_AfterInstall(sender As System.Object, e As System.Configuration.Install.InstallEventArgs) Handles ServiceProcessInstaller1.AfterInstall

    End Sub
End Class
