$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot)
$exe=Join-Path $root 'dist\windows\MidiToVocal.exe'
function WriteMidi($name,$tracks,$division=480,$format=1) {
  $b=[Collections.Generic.List[byte]]::new()
  $b.AddRange([byte[]](77,84,104,100,0,0,0,6,0,$format,0,$tracks.Count,($division -shr 8),($division -band 255)))
  foreach($t in $tracks){$b.AddRange([byte[]](77,84,114,107,0,0,0,$t.Count));$b.AddRange([byte[]]$t)}
  $path=Join-Path $PSScriptRoot $name
  [IO.File]::WriteAllBytes($path,$b.ToArray());return $path
}
function ConvertMidi($path,$out,$expect=0) {
  $p=Start-Process -FilePath $exe -ArgumentList @('--convert',('"'+$path+'"'),'135','7/8',('"'+$out+'"')) -Wait -PassThru
  if($p.ExitCode -ne $expect){throw "Unexpected exit: $($p.ExitCode)"}
  if($expect -eq 0){return (Get-Content $out -Raw | ConvertFrom-Json)}
}
function Assert($ok,$message){if(-not $ok){throw $message}}
$m=WriteMidi 'poly.mid' @(@(0,255,81,3,7,161,32,0,255,47,0),@(0,144,60,100,0,144,64,100,120,128,60,0,120,128,64,0,0,255,47,0),@(0,145,67,100,131,96,129,67,0,0,255,47,0))
$out=Join-Path $PSScriptRoot 'verified.svp'
$j=ConvertMidi $m $out
Assert ($j.tracks.Count -eq 3) 'Polyphony split failed'
Assert ($j.time.tempo[0].bpm -eq 135 -and $j.time.meter[0].numerator -eq 7 -and $j.time.meter[0].denominator -eq 8) 'Time settings failed'
for($i=0;$i -lt $j.tracks.Count;$i++){
  $t=$j.tracks[$i]
  Assert ($t.name -eq ('Melody'+($i+1))) 'Track naming failed'
  Assert ($t.mainRef.database.name -eq 'Mai' -and $t.mainRef.database.languageOverride -eq 'mandarin') 'Voice/language failed'
  foreach($n in $t.mainGroup.notes){Assert ($n.onset%88200000 -eq 0 -and $n.duration%88200000 -eq 0 -and $n.duration -gt 0) 'Grid failed'}
}
$c=$j.renderConfig
Assert ($c.numChannels -eq 2 -and $c.sampleRate -eq 48000 -and $c.bitDepth -eq 24 -and -not $c.exportMixDown -and $c.bypassGain -and $c.bypassPan -and $c.bypassEffects -and $c.aspirationFormat -eq 'noAspiration') 'Render settings failed'
$null=ConvertMidi $m $out
Assert ((Get-ChildItem "$out.*.bak").Count -ge 1) 'Backup failed'
# Format 0, UTF-8 lyric, running status, zero-velocity note off, very short note, drums.
$m=WriteMidi 'running.mid' @(,@(0,255,5,3,229,149,166,1,144,60,100,1,60,0,0,153,35,100,120,35,0,0,255,47,0)) 100 0
$j=ConvertMidi $m (Join-Path $PSScriptRoot 'running.svp')
Assert ($j.tracks.Count -eq 2) 'Drum preservation failed'
Assert ($j.tracks[0].mainGroup.notes[0].duration -eq 88200000) 'Short-note protection failed'
# Same-tick MIDI lyric.
$m=WriteMidi 'lyrics.mid' @(,@(0,255,5,3,229,149,166,0,144,60,100,120,128,60,0,0,255,47,0)) 480 0
$j=ConvertMidi $m (Join-Path $PSScriptRoot 'lyrics.svp')
Assert ($j.tracks[0].mainGroup.notes[0].lyrics -eq '啦') 'Lyric decoding failed'
$m=WriteMidi 'format2.mid' @(,@(0,255,47,0)) 480 2
ConvertMidi $m (Join-Path $PSScriptRoot 'format2.svp') 1
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'broken.mid'),[byte[]](77,84,104,100,0))
ConvertMidi (Join-Path $PSScriptRoot 'broken.mid') (Join-Path $PSScriptRoot 'broken.svp') 1
'PASS: polyphony, grid, tempo/meter, Mai/Mandarin, render settings, backups, running status, drums, short notes, UTF-8 lyrics, invalid files.'
