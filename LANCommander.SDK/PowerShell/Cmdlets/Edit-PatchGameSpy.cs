using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsData.Edit, "PatchGameSpy")]
public class EditPatchGameSpy : BaseCmdlet
{
    private const string GAMESPY_HOSTNAME = "gamespy.com";
    private const string GAMESPY_PUBLICKEY = "BF05D63E93751AD4A59A4A7389CF0BE8A22CCDEEA1E7F12C062D6E194472EFDA5184CCECEB4FBADF5EB1D7ABFE91181453972AA971F624AF9BA8F0F82E2869FB7D44BDE8D56EE50977898F3FEE75869622C4981F07506248BD3D092E8EA05C12B2FA37881176084C8F8B8756C4722CDC57D2AD28ACD3AD85934FB48D6B2D2027";
    
    private const string OPENSPY_HOSTNAME = "openspy.net";
    private const string OPENSPY_PUBLICKEY = "afb5818995b3708d0656a5bdd20760aee76537907625f6d23f40bf17029e56808d36966c0804e1d797e310fedd8c06e6c4121d963863d765811fc9baeb2315c9a6eaeb125fad694d9ea4d4a928f223d9f4514533f18a5432dd0435c5c6ac8e276cf29489cb5ac880f16b0d7832ee927d4e27d622d6a450cd1560d7fa882c6c13";
    
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; }

    [Parameter(Mandatory = false, Position = 1)]
    public string Hostname { get; set; } = OPENSPY_HOSTNAME;

    [Parameter(Mandatory = false, Position = 2)]
    public string PublicKey { get; set; } = OPENSPY_PUBLICKEY;
    
    [Parameter(Mandatory = false, Position = 3)]
    public string[] BinariesToPatch { get; set; } = { "*.dll", "*.exe", "*.ini" };
    
    [Parameter(Mandatory = false, Position = 4)]
    public string[] TextFilesToPatch { get; set; } = { "*.ini" };

    protected override void ProcessRecord()
    {
        var binaryMatcher = new Matcher();
        var textMatcher = new Matcher();
        
        binaryMatcher.AddIncludePatterns(BinariesToPatch);
        textMatcher.AddIncludePatterns(TextFilesToPatch);
        
        var binaryResults = binaryMatcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(Path)));
        var textResults = textMatcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(Path)));
        
        if (binaryResults.HasMatches)
            foreach (var binary in binaryResults.Files)
                PatchBinary(binary.Path);
        
        if (textResults.HasMatches)
            foreach (var text in textResults.Files)
                PatchText(text.Path);
    }

    private void PatchText(string path)
    {
        #region UT99 Games
        // https://openspy.net/howto/ut99-engine/ut
        //
        // 1) Replace GameSpy master server with OpenSpy anywhere it appears
        //    (covers both [UBrowserAll] and [Engine.GameEngine] ServerActors lines)
        TextFileHelper.ReplaceAll(
            path,
            @"MasterServerAddress\s*=\s*master0\.gamespy\.com",
            $"MasterServerAddress=master.{Hostname}",
            RegexOptions.IgnoreCase
        );

        // 2) Ensure bFallbackFactories=False inside [UBrowserAll] block
        //    This pattern:
        //    - Finds [UBrowserAll]
        //    - Consumes everything until the next [Section]
        //    - Finds "bFallbackFactories = True" inside that block
        //    - Replaces only the 'True' with 'False'
        TextFileHelper.ReplaceAll(
            path,
            @"(\[UBrowserAll\][^\[]*?bFallbackFactories\s*=\s*)True",
            "$1False",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        #endregion
        
        #region Unreal 2 Games
        // https://openspy.net/howto/ut2k-engine/ut2
        //
        // This pattern:
        // - Captures the [IpDrv.MasterServerLink] line + its newline in group 1
        // - Then consumes one or more MasterServerList=... lines after it
        // - We replace that whole chunk with the header + a single OpenSpy entry
        TextFileHelper.ReplaceAll(
            path,
            @"(\[IpDrv\.MasterServerLink\]\s*\r?\n)(?:\s*MasterServerList=.*\r?\n)+",
            $"$1MasterServerList=(Address=\"utmaster.{Hostname}\",Port=28902)\r\n",
            RegexOptions.IgnoreCase
        );
        #endregion
    }

    private void PatchBinary(string path)
    {
        if (Hostname.Length != GAMESPY_HOSTNAME.Length)
            throw new ArgumentOutOfRangeException(nameof(Hostname),
                $"Hostname must be {GAMESPY_HOSTNAME.Length} characters long");

        if (PublicKey.Length != GAMESPY_PUBLICKEY.Length)
            throw new ArgumentOutOfRangeException(nameof(PublicKey),
                $"Public key must be {GAMESPY_PUBLICKEY.Length} characters long");
        
        PatchBinary(path, GAMESPY_HOSTNAME, Hostname);
        PatchBinary(path, OPENSPY_PUBLICKEY, PublicKey);
        
        // TODO: AuthService requires a null character to be inserted at the end of the string
        // PatchBinary(path, "https://%s.auth.pubsvs", "http://%s.auth.pubsvs");
    }

    private void PatchBinary(string path, string pattern, string replacement)
    {
        IEnumerable<long> offsets;
        byte[] replacementData = Encoding.UTF8.GetBytes(replacement);
        
        using (var fs = new FileStream(path, FileMode.Open))
        {
            offsets = IndexOf(fs, pattern);

            if (!offsets.Any())
                return;
        }

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
        {
            foreach (var offset in offsets)
            {
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Write(replacementData, 0, replacementData.Length);
            }
        }
    }

    private IEnumerable<long> IndexOf(Stream stream, string pattern, int bufferSize = 8192)
        => IndexOf(stream, Encoding.UTF8.GetBytes(pattern), bufferSize);

    private IEnumerable<long> IndexOf(Stream stream, byte[] pattern, int bufferSize = 8192)
    {
        var buffer = new byte[bufferSize + pattern.Length - 1];
        long offset = 0;
        int tail = 0;

        while (true)
        {
            int bytesRead = stream.Read(buffer, tail, bufferSize);

            if (bytesRead == 0)
                break;
            
            int totalInBuffer = tail + bytesRead;
            int searchLimit = totalInBuffer - pattern.Length + 1;

            for (int i = 0; i < searchLimit; i++)
            {
                int j = 0;

                for (; j < totalInBuffer; j++)
                {
                    if (buffer[i + j] != pattern[j])
                        break;
                }

                if (j == pattern.Length)
                {
                    yield return offset + i;
                }
            }
            
            tail = Math.Min(pattern.Length - 1, totalInBuffer);
            {
                Buffer.BlockCopy(
                    buffer,
                    totalInBuffer - tail,
                    buffer,
                    0,
                    tail);
            }

            offset += totalInBuffer - tail;
        }
    }
}