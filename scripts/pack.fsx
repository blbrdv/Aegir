open System.IO
open System.IO.Compression


let args : string array = fsi.CommandLineArgs |> Array.tail

let ( !@ ) (paths : string seq) =
    let fullArray = 
        Seq.append [ __SOURCE_DIRECTORY__ ] paths
        |> Seq.toArray
    Path.Combine(fullArray)

let archive (name : string) =
    Directory.CreateDirectory(!@ [ @"..\out" ]) |> ignore

    let archivePath = !@ [ @"..\out"; $"{name}.zip"; ]

    File.Delete(archivePath)

    use archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)
    (
        archive.CreateEntryFromFile(
            !@ [ @"..\src\bin\Release\net46\Aegir.dll" ], 
            "Aegir.dll")
        |> ignore
        
        for filePath in Directory.GetFiles(!@ [ @"..\dist" ]) do
            archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath)) 
            |> ignore
    )


archive args.[0]
