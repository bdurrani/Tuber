namespace Tubesharp

module Youtube = 
    open System
    open System.Net
    open System.IO
    open System.Text
    open System.Linq
    open System.Web
    open System.Xml.Linq
    open Constants
    open Logging
    open System.Text.RegularExpressions
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq
    open Tubesharp.CoreInterfaces
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq
    open System.Text.RegularExpressions
    open System.Runtime.Serialization
    open System.Collections.Generic
    
    let RegexOptions = RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant ||| RegexOptions.Compiled
    
    ///checks to see if the video has an age restriction
    let GetAgeRestriction(page : string) = Regex.IsMatch(page, "player-age-gate-content")
    
    ///extract the thumbnail from the web page html
    let GetThumbnailFromPage webpage = 
        let regexstring = "<link itemprop=\"thumbnailurl\" href=(?<imageurl>.*)>"
        let regex = new Regex(regexstring, RegexOptions)
        //        let message = ("Unable to extract thumbnail from webpage")
        //        let message = Printf.TextWriterFormat<unit>("Unable to extract thumbnail from webpage")
        //https://stackoverflow.com/questions/18551851/why-does-fs-printfn-work-with-literal-strings-but-not-values-of-type-string
        let message = Printf.StringFormat<string, unit>("Unable to extract thumbnail from webpage")
        if regex.IsMatch(webpage) |> not then 
            ConsoleLogger.Log Warning message |> ignore
            Failure message
        else 
            let matchCollection = regex.Match(webpage)
            let result = matchCollection.Groups.Item("imageurl").Value
            Success result
    
    /// Parses the video url to extract its id
    let GetVideoId(url : string) = 
        let regex = new Regex("^.*v=(?'id'[\w\d\-]*)", RegexOptions)
        if (regex.IsMatch(url)) |> not then Failure "Unable to extract video id"
        else 
            let idMatchCollection = regex.Matches(url)
            let videoId = idMatchCollection.Item(0).Groups.Item("id").Value
            Success videoId
    
    ///Parse the text containing the video formats
    let FormatStringDict(formatstring : string) = 
        ///The default format type
        let DefaultFormat = 
            { Extension = String.Empty
              Height = None
              Width = None
              FormatNote = None
              Preference = None
              DASHVideo = None
              ACodec = None
              FPS = None
              VCodec = None
              ABR = None
              Container = None
              Protocol = None }
        
        ///create a format record from the json sub-objects
        let CreateFormatRecord(jProperty : JProperty) = 
            let defFmtType = DefaultFormat
            
            let populatedFmtType = 
                jProperty.Value.AsJEnumerable() |> Seq.fold (fun fmt x -> 
                                                       let jp = x :?> JProperty
                                                       let jVal = jp.Value :?> JValue
                                                       match jp.Name with
                                                       | "ext" -> { fmt with Extension = string jVal.Value }
                                                       | "width" -> 
                                                           { fmt with Width = Some <| int (jVal.Value.ToString()) }
                                                       | "height" -> 
                                                           { fmt with Height = Some <| int (jVal.Value.ToString()) }
                                                       | "format_note" -> 
                                                           { fmt with FormatNote = Some <| (jVal.Value.ToString()) }
                                                       | "preference" -> 
                                                           { fmt with Preference = Some <| int (jVal.Value.ToString()) }
                                                       | "acodec" -> 
                                                           { fmt with ACodec = Some <| (jVal.Value.ToString()) }
                                                       | "fps" -> { fmt with FPS = Some <| int (jVal.Value.ToString()) }
                                                       | "vcodec" -> 
                                                           { fmt with VCodec = Some <| (jVal.Value.ToString()) }
                                                       | "abr" -> { fmt with FPS = Some <| int (jVal.Value.ToString()) }
                                                       | "container" -> 
                                                           { fmt with Container = Some <| (jVal.Value.ToString()) }
                                                       | "protocol" -> 
                                                           { fmt with Protocol = Some <| (jVal.Value.ToString()) }
                                                       | _ -> fmt) defFmtType
            (jProperty.Name, populatedFmtType)
        
        let sb = new StringBuilder()
        let formatDict = new Dictionary<string, VideoFormat>()
        formatstring.Split([| '\n' |]) //get rid of any empty lines and comments
        |> Array.map (fun x -> x.Trim())
        |> Array.filter (fun x -> (String.IsNullOrWhiteSpace(x) |> not) && x.StartsWith("#") |> not)
        |> Array.iter (fun x -> sb.AppendLine(x) |> ignore)
        let jObj = sb.ToString() |> JObject.Parse
        jObj.Children<JProperty>()
        |> Seq.map (fun child -> CreateFormatRecord child)
        |> Seq.fold (fun (acc : Dictionary<string, VideoFormat>) current -> 
               acc.Add(fst current, snd current)
               acc) formatDict
    
    /// download the web page content
    let DownloadWebPage(url : string) = 
        try 
            let data = (new WebClient()).DownloadString(url)
            Success data
        with :? WebException as ex -> 
            printfn "Error: %s" ex.Message
            Failure ex.Message
    
    let Download url = 
        try 
            //set up a dictionary with the video format information. The id tag is the key
            let VideoFormatDictionary = FormatStringDict Constants.VideoFormats
            //extract the video id
            let ids = GetVideoId(url)
            
            let videoId = 
                match ids with
                | Success ids -> ids
                | _ -> 
                    ConsoleLogger.Log Error "unable to parse video id"
                    failwithf "unable to parse video id"
            
            ConsoleLogger.Log Information "Found video id: %s" videoId

            //build sanitized url from the id
            let cleanedUrl = sprintf "http://www.youtube.com/watch?v=%s&gl=US&hl=en&has_verified=1" videoId
            //download the web page html
            let webpageOption = DownloadWebPage cleanedUrl
            
            //check whether the function was successfull
            //exit the program if it was not
            //grab the contents of the page
            let webpage = 
                match webpageOption with
                | Success webpageOption -> webpageOption
                | Failure f -> 
                    failwithf "Unable to download webpage"

            if GetAgeRestriction(webpage) then 
                ConsoleLogger.Log Warning "Looks like the video might have an age restriction"

            ConsoleLogger.Log Information "Downloading video info"
            //line 684, youtube.py. get the video info 
            let rawInfo = 
                let VideoInfoUrl = 
                    "https://www.youtube.com/get_video_info?&video_id={0}&el=embedded&ps=default&eurl=&gl=US&hl=en"
                DownloadWebPage(String.Format(VideoInfoUrl, videoId))
            
            ConsoleLogger.Log Information "Download complete. Parsing video info"

            let optionPairs = 
                //parse the string and split the & separate items into a sequence
                //then split the = separated items into tuples
                let pairs (input : string) = 
                    input.Split [| '&' |]
                    |> Seq.ofArray
                    |> Seq.map (fun x -> 
                           let group = x.Split [| '=' |]
                           (group.[0], HttpUtility.UrlDecode group.[1]))
                match rawInfo with
                | Failure f -> 
                    ConsoleLogger.Log Error (Printf.StringFormat<unit, unit>(f))
                    failwithf (Printf.StringFormat<unit, unit>(f))
                    None
                | Success s -> Some(pairs s)
            
            let videoInfoDict = new Dictionary<string, string>()
            //populate the dictionary
            optionPairs.Value |> Seq.iter (fun x -> videoInfoDict.[fst x] <- snd x)
            ///Helper function to return the value for the key 
            let AccessOptionsDictKeyWithFailure key message fail = 
                if (videoInfoDict.ContainsKey(key)) then Some videoInfoDict.[key]
                else if fail then 
                    ConsoleLogger.Log Error message
                    failwithf message
                    None
                else 
                    if fail then ConsoleLogger.Log Warning message
                    None
            //if the status is set to failed, exit after printing out the reason
            if videoInfoDict.ContainsKey StatusKey then 
                let status = videoInfoDict.Item StatusKey
                if status = "fail" then 
                    let reason = 
                        AccessOptionsDictKeyWithFailure ReasonKey "Failed to get video information for unknown reason" 
                            true
                    ConsoleLogger.Log Error (Printf.StringFormat<unit, unit>(reason.Value))
                    failwithf (Printf.StringFormat<unit, unit>(reason.Value))

            ///helper function to return the value for the key if present in the dictionary
            ///and none otherwise
            let AccessOptionsDictKey key message = AccessOptionsDictKeyWithFailure key message false
            if videoInfoDict.ContainsKey RentalVideoKey then failwith "Rental videos not supported"
            //using the .net dictionary, so we need to deal with ref types
            let author = ref ""
            if (videoInfoDict.TryGetValue(AuthorKey, author) |> not) then failwith "Unable to get uploader name"
            //get the view count
            let viewcount = AccessOptionsDictKey ViewcountKey "Unable to get view count"
            
            //the video title
            let videoTitle = 
                let input = AccessOptionsDictKey TitleKey "unable to get video title"
                match input with
                | None -> ("_")
                | Some x -> x
            
            let imageurlFromPage = GetThumbnailFromPage webpage
            
            let imageurl = 
                match imageurlFromPage with
                | Success image -> Some image
                | Failure image -> AccessOptionsDictKey ThumbnailUrlKey "unable to find thumbnail url"
            
            let encodedurlmap = 
                sprintf "%s,%s" (videoInfoDict.Item(UrlEncodedFmtStreamMapKey)) (videoInfoDict.Item(AdaptiveFmtKey))
            
            //helper function to parse the url params
            let ParseUrlParams param = 
                let collection = HttpUtility.ParseQueryString param
                collection.AllKeys
                |> Array.map (fun x -> (x, collection.Item(x)))
                |> dict
            
            //parse the params into a dictionary
            let ParsedParamDictionary = encodedurlmap.Split [| ',' |] |> Array.map (fun x -> ParseUrlParams x)
            
            //this will help us sort the list of available formats
            //we want mp4 videos the most
            let ExtensionPref(fmt : VideoFormat) = 
                match fmt.Extension with
                | "webm" -> 0
                | "flv" -> 1
                | "mp4" -> 2
                | _ -> -1
            
            //sorting extractor/common.py, line 611
            //convert the parameter dictionary to a seq so we can sort it
            //and get the preferred video format
            let sortedVideo = 
                ParsedParamDictionary
                |> Seq.map (fun di -> 
                       let tag = di.Item "itag"
                       { FormatId = tag
                         Url = di.Item("url")
                         Format = VideoFormatDictionary.Item(tag) })
                |> Seq.sortBy (fun d -> d.Format.Preference)
                |> Seq.sortBy (fun d -> d.Format.Height)
                |> Seq.sortBy (fun d -> d.Format.Width)
                |> Seq.sortBy (fun d -> d.Format |> ExtensionPref)
            
            let selectedDl = Seq.last sortedVideo
            
            let cleanName input = 
                match input with
                | Some x -> x.ToString()
                | None -> "NA"
            
            let fileName = 
                let format = selectedDl.Format
                let width = cleanName format.Width
                let height = cleanName format.Height
                //get rid of spaces
                let title = videoTitle.Replace(' ', '-')
                sprintf "%s-%Ox%O.%s" title width height format.Extension
            
            //lets dl the file in a temp location
            printfn "Target file:%s" fileName
            let tmpFolder = Path.GetTempPath()
            let tmpFile = Path.Combine(tmpFolder, fileName) |> sprintf "%s.part"
            printf "%-20s" "Download progress:"
            let offset = 20 + 2
            let topOffset = Console.CursorTop
            Console.SetCursorPosition(offset, topOffset)
            use wc = new WebClient()
            do let obj = new obj()
               wc.DownloadProgressChanged |> Event.add (fun args -> 
                                                 //this event is raised on multiple different work threads
                                                 //and console was having trouble printing the results correct
                                                 //i had to serialize the printing
                                                 lock obj (fun _ -> 
                                                     Console.SetCursorPosition(offset, topOffset)
                                                     printf "%02i %%" args.ProgressPercentage))
            let asyncDl = 
                async { 
                    wc.DownloadFileAsync(new Uri(selectedDl.Url), tmpFile)
                    let! res = Async.AwaitEvent(wc.DownloadFileCompleted)
                    if res.Cancelled then printfn "Download cancelled"
                }
            
            let result = 
                asyncDl
                |> Async.Catch
                |> Async.RunSynchronously
            
            printfn ""
            match result with
            | Choice1Of2 x -> 
                (printfn "Download completed"
                 printfn "Copying over file from tmp location"
                 if File.Exists(fileName) then File.Delete(fileName)
                 File.Move(tmpFile, fileName)
                 Success fileName)
            | Choice2Of2 exn -> 
                (printfn "Exception occured while downloading:%s" exn.Message
                 Failure exn.Message)
        with ex -> Failure ex.Message
