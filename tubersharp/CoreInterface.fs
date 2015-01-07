namespace Tubesharp.CoreInterfaces

open System.Diagnostics

//experiment with error handling mechanism
type Result<'TSuccess, 'TFailure> = 
    | Success of 'TSuccess
    | Failure of 'TFailure

///Youtube video meta data
type YoutubeVideoInfo = 
    { Id : string
      Author : string
      Title : string
      Duration : int 
      ThumbnailUrl: string option
      }

///video format information
[<DebuggerDisplay("{ToString()}")>]
type VideoFormat = 
    { Extension : string
      Height : int option
      Width : int option
      FormatNote : string option
      Preference : int option
      DASHVideo : string option
      ACodec : string option
      FPS : int option
      VCodec : string option
      ABR : int option
      Container : string option
      Protocol : string option }
    override x.ToString() = 
        sprintf "Ext:%s Height:%A Width:%A" x.Extension x.Height x.Width

[<DebuggerDisplay("{FormatId}")>]
type Video = 
    { 
        FormatId:string 
        Url : string
        Format : VideoFormat 
    }

module RoP = 
    let bind switchFunction twoTrackInput = 
        match twoTrackInput with
        | Success s -> switchFunction s
        | Failure f -> Failure f
