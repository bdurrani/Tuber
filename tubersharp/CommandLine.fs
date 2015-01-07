namespace Tubesharp

module CommandLine = 
    open System
    open System.Text.RegularExpressions
    
    ///parsed command line options
    type CommandLineOptions = 
        { DisplayHelp : bool
          Url : string }
    
    ///option is the string from the command line
    ///param1 is the string that we are testing the command line against
    let (|ValidOption|_|) (param1 : string) (option : string) = 
        let fixcase = option.ToLowerInvariant()
        match fixcase.StartsWith(param1) with
        | true -> Some(option)
        | false -> None
    
    //displays the command line options available
    let printHelp () = 
        let exeName = AppDomain.CurrentDomain.FriendlyName
        printfn "Usage: %s [options] url" exeName
        printfn "\nOptions:"
        printfn "%-8s%20s" "-h" "Prints this text and exits"
    
    //adding support for parsing command line
    let parseItem str options = 
        match str with
        | ValidOption "-h" op -> { options with DisplayHelp = true }
        | ValidOption "http" op -> { options with Url = op.Trim() }
        | _ -> options

    let defaultOptions = 
        { DisplayHelp = false
          Url = "" }
    
    let rec parseCommandLine args optionsSoFar = 
        match args with
        | [] -> optionsSoFar
        | x :: xs -> parseCommandLine xs (parseItem x optionsSoFar)
    
    let parseCommandLineWithDefaults args = parseCommandLine args defaultOptions

    /// <summary>
    /// tests whether the input url is valid
    /// </summary>
    /// <param name="url"></param>
    let (|ValidYoutubeUrl|_|) url = 
        let regexExpression = "^http(s)?://www\.youtube\.com.*watch\?v="
        let regMatch = Regex.IsMatch(url,regexExpression, RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)
        if regMatch then Some(url)
        else None
