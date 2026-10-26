#!/usr/bin/env python3
"""Compose two original, deterministic orchestral scores and render with FluidR3 GM.

No existing MIDI, melody or audio recording is used. FluidR3 samples are MIT.
Requires libfluidsynth.so.3, numpy, scipy and ffmpeg. The large build-only
SoundFont stays outside the repository; download-soundfont.py reproduces it.
"""
from pathlib import Path
import argparse, ctypes as C, hashlib, json, math, random, struct, subprocess
import numpy as np
from scipy.io import wavfile

ROOT=Path(__file__).resolve().parents[2]
RATE=44100
FONT_SHA='74594e8f4250680adf590507a306655a299935343583256f3b722c48a1bc1cb0'

class Score:
    def __init__(self,name,tempo,bars,programs):
        self.name,self.tempo,self.bars,self.programs=name,tempo,bars,programs
        self.events=[]; self.notes=[]; self.rng=random.Random(731029 if name=='starlight-overture' else 621713)
    def note(self,ch,pitch,start,duration,velocity):
        # Fixed score, with bounded human expression rather than random pitches.
        velocity=max(1,min(115,round(velocity+self.rng.uniform(-3,3))))
        t=max(0,start + (self.rng.uniform(-.009,.009) if start%4 else 0))
        end=min(self.bars*4-.015,t+duration)
        self.events += [(t,1,ch,pitch,velocity),(end,0,ch,pitch,0)]
        self.notes.append({'channel':ch,'pitch':pitch,'beat':round(t,5),'duration':round(end-t,5),'velocity':velocity})
    @property
    def seconds(self):return self.bars*4*60/self.tempo

# Explicit harmonic/melodic material; General MIDI program numbers are zero based.
FC_CHORDS=[(38,[50,57,62,65,69]),(34,[53,58,62,65,69]),(41,[53,60,65,69,72]),(36,[52,55,60,64,67]),
           (31,[50,55,58,62,65]),(33,[53,57,62,65,69]),(34,[53,58,62,65,69]),(33,[52,57,61,64,69])]
FC_THEME=[[(74,1.5),(69,.5),(77,1),(76,.5),(74,.5)],[(77,1),(74,1),(70,2)],
          [(69,.5),(72,.5),(77,1),(79,1),(81,1)],[(79,1),(76,1),(72,1),(67,1)],
          [(70,1),(74,1),(79,1),(77,1)],[(77,1),(76,.5),(74,.5),(69,2)],
          [(74,1),(77,1),(74,1),(72,.5),(70,.5)],[(76,1),(73,1),(69,1),(73,1)]]
FC_BRIDGE=[[(77,2),(76,1),(72,1)],[(76,1),(79,1),(76,1),(72,1)],[(74,1.5),(77,.5),(81,2)],[(77,1),(74,1),(70,2)],
           [(74,1),(79,1),(82,1),(81,1)],[(81,1),(77,1),(74,2)],[(77,1.5),(74,.5),(70,1),(72,1)],[(73,2),(76,1),(73,1)]]

def fantasy():
    s=Score('starlight-overture',112,48,{0:48,1:60,2:43,3:46,4:45,5:73,6:47,7:61,8:0})
    for b in range(s.bars):
        section=b//16; phrase=b%8; root,chord=FC_CHORDS[phrase]
        if section==1 and phrase<4:
            root,chord=[(41,[53,60,65,69,72]),(40,[52,55,60,64,67]),FC_CHORDS[0],FC_CHORDS[1]][phrase]
        full= b>=8 and section!=1; strength=1.12 if section==2 else .92 if section==1 else 1
        start=4*b
        # Broad five-voice strings, independent cello/bass foundation.
        for j,n in enumerate(chord):s.note(0,n,start+j*.018,3.85,51*strength+(j==4)*4)
        s.note(2,root,start,1.82,65*strength)
        s.note(2,root+12,start+2,1.82,56*strength)
        # Shimmering harp broken chords; continuous but quiet enough to leave the melody clear.
        arpeggio=[chord[0]+12,chord[2]+12,chord[3]+12,chord[4]+12,chord[2]+12,chord[3]+12,chord[4]+12,chord[1]+24]
        for k,n in enumerate(arpeggio):s.note(3,n,start+k*.5,.75,38+(k%4==0)*9)
        if full:
            for k in range(8):s.note(4,chord[(k//2)%3]+12,start+k*.5,.27,42+(k%2==0)*9)
        melody=FC_BRIDGE[phrase] if section==1 else FC_THEME[phrase]
        offset=0
        for n,d in melody:
            lead=5 if section==1 else 1
            s.note(lead,n,start+offset,max(.12,d-.10),65*strength)
            # Final chorus doubles the longer lyrical gestures in strings, not every attack.
            if section==2 and d>=1:s.note(7,n-12,start+offset,d-.14,45)
            offset+=d
        if b%16>=8:
            s.note(8,chord[2]+12,start+.04,1.7,43)
            s.note(8,chord[4]+12,start+2.04,1.7,39)
        # Measured timpani and orchestral drum accents; no dance kick or harsh hi-hat loop.
        s.note(6,root if root>=38 else root+12,start,1.55,48*strength)
        if full:s.note(6,(root if root>=38 else root+12)+7,start+2,1.45,42*strength)
        if b%4==0:s.note(9,49,start,2.8,42 if section!=1 else 25)
        if section!=1:
            for beat,vel in [(0,54),(2,45)]:s.note(9,36,start+beat,.55,vel*strength)
            if full:
                for beat in [1,3]:s.note(9,41,start+beat,.5,36*strength)
        if phrase==7:
            for k in range(4):s.note(6,root if root>=38 else root+12,start+3+k*.23,.2,34+5*k)
    return s

POD_CHORDS=[(43,[55,59,62,66,69]),(36,[52,55,59,64,67]),(40,[52,55,59,62,66]),(38,[54,57,62,66,69]),
            (33,[52,57,60,64,67]),(35,[55,59,62,66,69]),(36,[52,55,59,64,67]),(38,[54,57,60,62,69])]
POD_THEME=[[(67,1),(69,.5),(71,.5),(74,2)],[(76,1.5),(74,.5),(71,1),(67,1)],
           [(67,.5),(71,.5),(76,1),(74,1),(71,1)],[(69,1),(74,1),(78,1),(76,1)],
           [(72,1),(71,.5),(69,.5),(76,1),(72,1)],[(71,2),(74,1),(79,1)],
           [(76,1),(74,1),(71,1),(69,1)],[(66,1),(69,1),(72,1),(69,1)]]
POD_BRIDGE=[[(74,1.5),(71,.5),(69,2)],[(71,1),(76,1),(79,2)],[(78,1),(76,1),(74,1),(71,1)],[(69,2),(66,2)],
            [(72,1.5),(76,.5),(79,1),(76,1)],[(74,1),(71,1),(67,2)],[(71,1),(74,1),(76,1),(74,1)],[(69,1.5),(72,.5),(74,1),(66,1)]]

def dawn():
    s=Score('dawn-on-galilee',92,40,{0:73,1:0,2:48,3:46,4:42,5:68,6:60,7:45})
    for b in range(s.bars):
        phrase=b%8;section=b//8; root,chord=POD_CHORDS[phrase];start=b*4
        strength=[.8,.95,.83,1.0,.88][section]
        for j,n in enumerate(chord):s.note(2,n,start+j*.025,3.85,40*strength+(j==4)*3)
        s.note(4,root if root>=36 else root+12,start,3.8,43*strength)
        # Piano spreads and harp answer each other, with air around the flute melody.
        for j,n in enumerate(chord[:4]):s.note(1,n,start+.035*j,1.9,35+(j==3)*6)
        s.note(1,chord[3]+12,start+2,1.8,34)
        for k,i in enumerate([0,2,3,4,2,3,4,2]):s.note(3,chord[i]+12,start+k*.5,.9,31+(k%4==0)*7)
        melody=POD_BRIDGE[phrase] if section==2 else POD_THEME[phrase]
        lead=5 if section in [1,3] else 0
        offset=0
        for n,d in melody:
            s.note(lead,n,start+offset,d-.09,61*strength)
            offset+=d
        if section in [1,3]:
            for n in chord[1:4]:s.note(6,n,start+2,1.8,26)
            for k in [0,2]:s.note(7,chord[1],start+k,.45,34)
        if b%4==0:s.note(9,81,start,1.8,31)
        if section==3 and b%2==0:s.note(9,36,start,.65,29)
    return s

def variable(n):
    out=[n&127];n>>=7
    while n:out.insert(0,(n&127)|128);n>>=7
    return bytes(out)

def write_midi(score,path):
    ticks=480
    meta=b'\x00\xff\x03'+variable(len(score.name))+score.name.encode()
    tempo=round(60000000/score.tempo)
    meta+=b'\x00\xff\x51\x03'+tempo.to_bytes(3,'big')+b'\x00\xff\x58\x04\x04\x02\x18\x08\x00\xff\x2f\x00'
    tracks=[meta]
    for ch in sorted(set(e[2] for e in score.events)):
        stream=b'\x00'+bytes([0xc0+ch,score.programs.get(ch,0)])
        stream+=b'\x00'+bytes([0xb0+ch,7,100])+b'\x00'+bytes([0xb0+ch,91,35])
        last=0
        for beat,on,channel,n,vel in sorted(e for e in score.events if e[2]==ch):
            at=round(beat*ticks)
            stream+=variable(at-last)+bytes([(0x90 if on else 0x80)+ch,n,vel]);last=at
        stream+=variable(score.bars*4*ticks-last)+b'\xff\x2f\x00'
        tracks.append(stream)
    path.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(tracks),ticks)+b''.join(b'MTrk'+struct.pack('>I',len(t))+t for t in tracks))

def fluidsynth():
    f=C.CDLL('libfluidsynth.so.3')
    sigs={'new_fluid_settings':([],C.c_void_p),'delete_fluid_settings':([C.c_void_p],None),
          'fluid_settings_setnum':([C.c_void_p,C.c_char_p,C.c_double],C.c_int),'fluid_settings_setint':([C.c_void_p,C.c_char_p,C.c_int],C.c_int),
          'new_fluid_synth':([C.c_void_p],C.c_void_p),'delete_fluid_synth':([C.c_void_p],None),
          'fluid_synth_sfload':([C.c_void_p,C.c_char_p,C.c_int],C.c_int),
          'fluid_synth_program_change':([C.c_void_p,C.c_int,C.c_int],C.c_int),
          'fluid_synth_cc':([C.c_void_p,C.c_int,C.c_int,C.c_int],C.c_int),
          'fluid_synth_noteon':([C.c_void_p,C.c_int,C.c_int,C.c_int],C.c_int),
          'fluid_synth_noteoff':([C.c_void_p,C.c_int,C.c_int],C.c_int),
          'fluid_synth_write_float':([C.c_void_p,C.c_int,C.c_void_p,C.c_int,C.c_int,C.c_void_p,C.c_int,C.c_int],C.c_int)}
    for name,(args,result) in sigs.items():getattr(f,name).argtypes=args;getattr(f,name).restype=result
    return f

def render(score,soundfont,work):
    f=fluidsynth();settings=f.new_fluid_settings()
    for key,value in [('synth.sample-rate',RATE),('synth.gain',.45),('synth.reverb.room-size',.72),('synth.reverb.damp',.38),('synth.reverb.width',80),('synth.reverb.level',.28),('synth.chorus.level',.12)]:
        f.fluid_settings_setnum(settings,key.encode(),value)
    f.fluid_settings_setint(settings,b'synth.polyphony',256)
    synth=f.new_fluid_synth(settings)
    assert f.fluid_synth_sfload(synth,str(soundfont).encode(),1)>=0
    for ch,program in score.programs.items():
        assert f.fluid_synth_program_change(synth,ch,program)==0
    for ch in range(16):
        f.fluid_synth_cc(synth,ch,7,100)
        f.fluid_synth_cc(synth,ch,91,34)
        f.fluid_synth_cc(synth,ch,93,10)
        f.fluid_synth_cc(synth,ch,10,{0:48,1:65,2:76,3:92,4:36,5:79,6:60,7:45,8:67,9:64}.get(ch,64))
    count=round(score.seconds*RATE)
    # Render two cycles with the reverb/tails intact; take the second complete score.
    events=[]
    for cycle in range(2):
        for beat,on,ch,n,vel in score.events:events.append((cycle*count+round(beat*60/score.tempo*RATE),on,ch,n,vel))
    events.sort();events.append((2*count,0,15,0,0))
    audio=np.zeros((2*count,2),dtype=np.float32)
    cursor=0
    for at,on,ch,n,vel in events:
        length=at-cursor
        if length>0:
            left=np.empty(length,dtype=np.float32);right=np.empty(length,dtype=np.float32)
            f.fluid_synth_write_float(synth,length,left.ctypes.data,0,1,right.ctypes.data,0,1)
            audio[cursor:at,0]=left;audio[cursor:at,1]=right;cursor=at
        if at>=2*count:break
        if on:f.fluid_synth_noteon(synth,ch,n,vel)
        else:f.fluid_synth_noteoff(synth,ch,n)
    f.delete_fluid_synth(synth);f.delete_fluid_settings(settings)
    clip=audio[count:].copy()
    clip-=clip.mean(axis=0)
    wav=work/(score.name+'-render.wav');wavfile.write(wav,RATE,clip)
    return wav

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--soundfont',type=Path,default=Path('/workspace/tools/audio/fluid-r3/usr/share/sounds/sf2/FluidR3_GM.sf2'))
    parser.add_argument('--work',type=Path,default=Path('/tmp/firstgame-music'))
    args=parser.parse_args();assert hashlib.sha256(args.soundfont.read_bytes()).hexdigest()==FONT_SHA
    args.work.mkdir(parents=True,exist_ok=True)
    (ROOT/'Tools/Audio/Scores').mkdir(exist_ok=True)
    report={'composition':'Original deterministic scores composed for FirstGame; no pre-existing MIDI, melody or recording used.','sampleRate':RATE,'soundfontSha256':FONT_SHA,'tracks':[]}
    for game,score in [('FantasyCards',fantasy()),('PathOfDawn',dawn())]:
        midi=ROOT/'Tools/Audio/Scores'/f'{score.name}.mid';write_midi(score,midi)
        (midi.with_suffix('.json')).write_text(json.dumps({'title':score.name,'tempo':score.tempo,'bars':score.bars,'programs':score.programs,'notes':score.notes},indent=2)+'\n')
        wav=render(score,args.soundfont,args.work)
        # Broadcast-style normalization is offline; 3ms seam ramps prevent a sample discontinuity.
        normalized=args.work/(score.name+'-normalized.wav')
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(wav),'-af','loudnorm=I=-18:TP=-1.5:LRA=11','-ar',str(RATE),'-c:a','pcm_f32le',str(normalized)],check=True)
        rate,samples=wavfile.read(normalized); ramp=min(133,len(samples)//10)
        samples[:ramp]*=np.linspace(0,1,ramp)[:,None];samples[-ramp:]*=np.linspace(1,0,ramp)[:,None]
        assert np.isfinite(samples).all() and np.max(np.abs(samples))<1
        finalwav=args.work/(score.name+'.wav');wavfile.write(finalwav,rate,samples)
        target=ROOT/f'Games/{game}/Assets/Resources/Audio/{score.name}.ogg';target.parent.mkdir(parents=True,exist_ok=True)
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-i',str(finalwav),'-c:a','libvorbis','-q:a','5','-metadata','title='+('Starlight Overture' if game=='FantasyCards' else 'Dawn on Galilee'),'-metadata','artist=FirstGame original score','-metadata','comment=Original composition; FluidR3 GM sample performance (MIT)',str(target)],check=True)
        preview=args.work/(score.name+'-preview.ogg')
        subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-ss','68','-i',str(target),'-t','15','-c:a','libvorbis','-q:a','4',str(preview)],check=True)
        report['tracks'].append({'game':game,'file':str(target.relative_to(ROOT)),'title':score.name,'seconds':round(len(samples)/rate,6),'tempo':score.tempo,'bars':score.bars,'noteCount':len(score.notes),'bytes':target.stat().st_size,'sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'midiSha256':hashlib.sha256(midi.read_bytes()).hexdigest(),'peakDbFS':round(float(20*np.log10(np.max(np.abs(samples)))),3),'rmsDbFS':round(float(20*np.log10(np.sqrt(np.mean(samples*samples)))),3),'loopMethod':'Second complete musical cycle retains ambience tails; dominant-to-tonic cadence, no added silence, 3ms endpoint ramps.','preview':str(preview)})
        print(json.dumps(report['tracks'][-1]),flush=True)
    (ROOT/'Tools/Audio/production.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')

if __name__=='__main__':main()
