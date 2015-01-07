namespace Tubesharp

module Main = 
    open System
    open Logging
    open Youtube
    open Tubesharp.CoreInterfaces
    open CommandLine
    
    
    [<EntryPoint>]
    let main argv = 
        
        let commandLineOptions = parseCommandLineWithDefaults (List.ofArray argv)
        if commandLineOptions.DisplayHelp then 
            printHelp ()
            exit 0
        //validate the inputs
        let input = 
            match commandLineOptions.Url with
            | ValidYoutubeUrl url -> url
            | _ -> 
                (printfn "youtube url (%s) not valid. Exiting" commandLineOptions.Url
                 exit 0
                 "")
        //let input = "https://www.youtube.com/watch?v=cWz5uYWnI5k"
        let filename = Download input 
        match filename with
            | Success x -> printfn "Download successful: %s" x
            | Failure x-> printfn "Download failed:%s" x

        0 // return an integer exit code
