"""Original stylized character/environment revision, Blender 4.3+.

Uses the first-slice generator's mesh/export helpers, but replaces all four
PathOfDawn FBXs. Does not touch their Unity .meta files or FantasyCards assets.
Run: blender --background --python Tools/Art/generate_toon_dawn.py -- --root . --render
"""
from pathlib import Path
import json

_base = Path(__file__).with_name('generate_proxies.py')
exec(compile(_base.read_text().split('\nshore=build_shore()')[0], str(_base), 'exec'))
previous = json.loads((OUT/'AssetManifest.json').read_text())
MANIFEST['assets'] = [a for a in previous['assets'] if '/FantasyCards/' in a['path']]
MANIFEST['generator'] = 'Tools/Art/generate_toon_dawn.py'
MANIFEST['status'] = 'Original stylized revision; authored procedural meshes, review candidate, not approved final character art'
MANIFEST['revision'] = 'toon-dawn-01'
random.seed(231023)

palette = {
 'sand':('POD_SunlitLimestone',(.71,.56,.34)),
 'stone':('POD_WarmStone',(.68,.55,.40)),
 'stone_light':('POD_ChalkHighlights',(.85,.73,.53)),
 'stone_dark':('POD_StoneShadow',(.46,.39,.31)),
 'water':('POD_GalileeTeal',(.10,.47,.53)),
 'foam':('POD_ShoreFoam',(.59,.85,.80)),
 'wood':('POD_OliveWood',(.23,.135,.075)),
 'woodlight':('POD_OakHighlights',(.46,.265,.115)),
 'olive':('POD_OliveFoliage',(.19,.32,.14)),
 'olive_light':('POD_OliveSunlit',(.44,.55,.24)),
 'reed':('POD_ReedOchre',(.58,.56,.23)),
 'terracotta':('POD_Terracotta',(.68,.29,.16)),
 'cloth':('POD_UnbleachedLinen',(.95,.88,.70)),
 'red':('POD_EarthRedMantle',(.63,.145,.095)),
 'blue':('POD_IndigoLinen',(.075,.32,.40)),
 'belt':('POD_RopeAndLeather',(.24,.125,.06)),
 'skin':('POD_WarmOliveSkin',(.64,.365,.215)),
 'skinlight':('POD_SkinHighlights',(.73,.45,.29)),
 'hair':('POD_DarkBrownHair',(.065,.032,.019)),
 'eyes':('POD_DarkEyes',(.012,.013,.013)),
 'gold':('POD_WovenOchreTrim',(.76,.48,.16)),
 'creamshadow':('POD_LinenFoldShadow',(.72,.61,.43)),
 'blueshadow':('POD_IndigoFoldShadow',(.04,.19,.25)),
 'redshadow':('POD_MantleFoldShadow',(.39,.065,.045)),
 'eyewhite':('POD_EyeIvory',(.98,.94,.84)),
 'iris':('POD_HazelIris',(.21,.125,.045)),
 'lip':('POD_MutedRoseLips',(.43,.16,.12)),
 'innerskin':('POD_EarAndLidWarmth',(.53,.25,.16)),
 'miriamskin':('POD_MiriamOliveSkin',(.79,.53,.36)),
 'miriamblush':('POD_MiriamCheekWarmth',(.73,.37,.27)),
 'hairshine':('POD_ChestnutHairLight',(.12,.052,.026)),
 'leafsilver':('POD_OliveLeafSilver',(.42,.55,.36)),
 'grass':('POD_SageGroundcover',(.31,.43,.23)),
 'flowers':('POD_WildflowerCream',(.96,.83,.50)),
}
for key,(name,color) in palette.items():
    if key in M and M[key].name==name:
        mat=M[key]; mat.diffuse_color=(*color,1)
        mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
    else: M[key]=material(name,color)

def smooth(obj):
    if obj.type=='MESH':
        for p in obj.data.polygons:p.use_smooth=True
    return obj

def uv(name,loc,scale,mat,segments=24,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=loc)
    obj=assign(bpy.context.object,name,mat);obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return smooth(obj)

def bevelbox(name,loc,scale,mat,bevel=.015):
    obj=cube(name,loc,scale,mat)
    m=obj.modifiers.new('Crafted_edges','BEVEL');m.width=bevel;m.segments=3
    bpy.ops.object.modifier_apply(modifier=m.name)
    m=obj.modifiers.new('Weighted_corner_normals','WEIGHTED_NORMAL');m.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=m.name)
    return obj

def loft(name,rings,mat,n=48,pleats=0,center=(0,0)):
    # z, half-width, front depth, back depth, fold amplitude.
    verts=[];faces=[]
    for z,w,front,back,amp in rings:
        for i in range(n):
            t=i*math.tau/n;c=math.cos(t);fold=1+amp*math.cos(pleats*t+.3) if pleats else 1
            verts.append((center[0]+math.sin(t)*w*fold,center[1]-c*(front if c>=0 else back)*fold,z))
    for j in range(len(rings)-1):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
    faces.append(tuple(reversed(range(n))));faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
    return smooth(mesh(name,verts,faces,mat))

def tube(name,points,radii,mat,segments=10,depth_ratio=1):
    raw=[Vector(p) for p in points]
    # Catmull-Rom centerline and transported frames avoid angular noodles and
    # sudden ribbon twists at a reference-axis threshold.
    pts=[];rs=[]
    if len(raw)>2:
        for j in range(len(raw)-1):
            a=raw[max(0,j-1)];b=raw[j];c=raw[j+1];d=raw[min(len(raw)-1,j+2)]
            for step in range(4):
                t=step/4
                pts.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
                rs.append(radii[j]*(1-t)+radii[j+1]*t)
        pts.append(raw[-1]);rs.append(radii[-1])
    else:pts=raw;rs=radii
    verts=[];faces=[];last_tangent=None;u=None
    for j,p in enumerate(pts):
        tangent=(pts[min(j+1,len(pts)-1)]-pts[max(j-1,0)]).normalized()
        if u is None:
            ref=Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((1,0,0));u=tangent.cross(ref).normalized()
        else:u=last_tangent.rotation_difference(tangent)@u
        v=tangent.cross(u).normalized();last_tangent=tangent
        for i in range(segments):
            t=i*math.tau/segments;q=p+(u*math.cos(t)+v*math.sin(t)*depth_ratio)*rs[j];verts.append(q)
    for j in range(len(pts)-1):
        for i in range(segments):a=j*segments+i;b=j*segments+(i+1)%segments;faces.append((a,b,b+segments,a+segments))
    faces.extend([tuple(reversed(range(segments))),tuple((len(pts)-1)*segments+i for i in range(segments))])
    return smooth(mesh(name,verts,faces,mat))

def curve(name,points,radius,mat):
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.resolution_u=10
    spline=data.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
    for p,co in zip(spline.bezier_points,points):p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    data.bevel_depth=radius;data.bevel_resolution=2;data.resolution_u=6
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);data.materials.append(M[mat])
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj;bpy.ops.object.convert(target='MESH')
    return obj

def patch(name,rows,mat):
    verts=[p for row in rows for p in row];n=len(rows[0]);faces=[]
    for j in range(len(rows)-1):
        for i in range(n-1):a=j*n+i;faces.append((a,a+1,a+n+1,a+n))
    return smooth(mesh(name,verts,faces,mat))

def cloth_surface(obj,front=False):
    if front:
        for p in obj.data.polygons:p.flip()
    mod=obj.modifiers.new('ThinWovenCloth','SOLIDIFY');mod.thickness=.002;mod.offset=0
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def skin_weights(obj,weights):
    for name in weights:
        if not obj.vertex_groups.get(name):obj.vertex_groups.new(name=name)
    for vertex in obj.data.vertices:
        pos=obj.matrix_world@vertex.co
        values=weights if not callable(next(iter(weights.values()))) else {k:f(pos) for k,f in weights.items()}
        for name,value in values.items():
            if value>.0001:obj.vertex_groups[name].add([vertex.index],value,'REPLACE')
    return obj

def join_objects(objects,name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name
    return obj

def character(name,height,kind):
    # FBX's single baked take uses the scene name; preserve the established clip.
    scene=new_scene(name+'_BasicIdle')
    pieces=[]
    bodymat='cloth' if kind=='jesus' else 'blue'
    skinmat='miriamskin' if kind=='miriam' else 'skinlight'
    def part(obj,bone='Head'):
        pieces.append(obj)
        return skin_weights(obj,{bone:1})
    # A continuous tailored garment, with designed silhouette and woven pleats.
    skirt_bottom=.14 if kind!='traveler' else .67
    if kind=='jesus':
        rings=[(.14,.255,.145,.14,.045),(.21,.256,.145,.14,.04),(.48,.232,.14,.14,.055),(.76,.205,.135,.13,.04),(1.00,.165,.105,.10,.018),(1.04,.163,.102,.10,.015),(1.16,.19,.12,.11,.012),(1.30,.213,.12,.11,.006),(1.42,.216,.095,.09,.003),(1.47,.15,.077,.07,0),(1.49,.062,.052,.05,0)]
    elif kind=='miriam':
        rings=[(.13,.27,.16,.155,.045),(.19,.265,.155,.15,.045),(.50,.23,.145,.14,.05),(.80,.195,.13,.12,.04),(1.02,.135,.092,.09,.015),(1.08,.14,.095,.09,.01),(1.20,.165,.12,.10,.008),(1.34,.18,.11,.09,.004),(1.43,.19,.075,.07,0),(1.47,.12,.065,.06,0),(1.49,.058,.045,.045,0)]
    else:
        rings=[(.67,.23,.135,.135,.025),(.72,.225,.13,.13,.02),(.87,.195,.12,.12,.02),(1.03,.16,.105,.1,.01),(1.09,.165,.11,.1,.01),(1.24,.195,.12,.105,.006),(1.40,.22,.10,.09,0),(1.46,.16,.075,.07,0),(1.49,.06,.05,.05,0)]
    body=loft(name+'_TailoredRobe',rings,bodymat,64,14);garment=body;sleeves=[]
    blend=lambda p:max(0,min(1,(p.z-1.02)/.24))
    pieces.append(skin_weights(body,{'Spine':blend,'Pelvis':lambda p:1-blend(p)}))
    # Hem and collar are modeled sewn bands, not intersecting rectangular slabs.
    low=rings[0];part(loft(name+'_WovenHem',[(low[0],low[1]+.002,low[2]+.002,low[3]+.002,low[4]),(low[0]+.028,low[1]+.002,low[2]+.002,low[3]+.002,low[4])],'gold' if kind!='jesus' else 'creamshadow',64,14),'Pelvis')
    part(loft(name+'_Neck',[(1.45,.047,.045,.044,0),(1.50,.046,.044,.043,0),(1.555,.05,.047,.047,0)],skinmat,32),'Head')
    # Profiled face with a jaw, cheekbones and forehead; around seven heads tall.
    headrings=[(1.526,.026,.051,.032,0),(1.539,.048,.069,.048,0),(1.56,.068,.076,.063,0),(1.59,.080,.081,.073,0),(1.625,.091,.082,.078,0),(1.66,.093,.079,.081,0),(1.695,.091,.077,.083,0),(1.728,.082,.071,.078,0),(1.752,.061,.057,.062,0),(1.767,.026,.029,.032,0),(1.77,.003,.005,.006,0)]
    if kind=='miriam':headrings=[(z,w*.94,f,b,a) for z,w,f,b,a in headrings]
    dense=[]
    for a,b in zip(headrings,headrings[1:]):
        for i in range(3):dense.append(tuple(a[k]*(1-i/3)+b[k]*(i/3) for k in range(5)))
    dense.append(headrings[-1]);head=loft(name+'_SculptedFace',dense,skinmat,64)
    # The nose is part of the continuous facial surface, not an attached primitive.
    nasal=[(1.590,0),(1.600,.004),(1.610,.025),(1.625,.022),(1.650,.011),(1.680,.002),(1.695,0)]
    if kind=='miriam':nasal=[(z+.008,amount*.78) for z,amount in nasal]
    for vertex in head.data.vertices:
        x,y,z=vertex.co
        if y<-.035:
            for (za,aa),(zb,ab) in zip(nasal,nasal[1:]):
                if za<=z<=zb:
                    amount=aa+(ab-aa)*(z-za)/(zb-za)
                    vertex.co.y-=amount*math.exp(-((x/(.015 if kind=='miriam' else .017))**2))
                    break
    # One subdivision keeps deliberate cheek/jaw planes while removing faceted seams.
    sub=head.modifiers.new('Facial_surface','SUBSURF');sub.levels=1;bpy.context.view_layer.objects.active=head;bpy.ops.object.modifier_apply(modifier=sub.name)
    part(head)
    for side in (-1,1):
        part(uv(name+'_Ear',(side*.090,0,1.627),(.011,.009,.025),skinmat))
        part(uv(name+'_EarConcha',(side*.096,-.007,1.627),(.0045,.002,.013),'innerskin'))
        # Almond eyes: a convex surface with independently modeled lid contours.
        cx=side*.037;cz=1.663;w=.027 if kind=='miriam' else .025;h=.010 if kind=='miriam' else .008
        face_y=lambda x:-.080*math.sqrt(max(.05,1-(x/.095)**2))-.002
        rows=[]
        for j in range(9):
            v=-1+j/4;row=[]
            for i in range(17):
                u=-1+i/8;shape=max(0,1-u*u)**.58
                x=cx+u*w;z=cz+v*h*shape+side*u*.003
                y=face_y(x)-.002*(1-u*u)*(1-v*v)
                row.append((x,y,z))
            rows.append(row)
        part(patch(name+'_EyeWhite',rows,'eyewhite'))
        eyey=face_y(cx)-.003
        iris=part(uv(name+'_Iris',(cx,eyey-.0008,cz),(.008,.0014,.0088),'iris',32,12));iris.rotation_euler.z=side*.36
        pupil=part(uv(name+'_Pupil',(cx,eyey-.0024,cz+.0002),(.004,.0010,.0051),'eyes',24,12));pupil.rotation_euler.z=side*.36
        part(uv(name+'_EyeGlint',(cx-.0023,eyey-.0035,cz+.0035),(.0015,.0006,.0017),'eyewhite',12,8))
        upper=[];lower=[]
        for i in range(9):
            u=-1+i/4;x=cx+u*w;shape=max(0,1-u*u)**.58;y=face_y(x)-.0005
            upper.append((x,y-.001,cz+h*shape+side*u*.003));lower.append((x,y,cz-h*shape+side*u*.003))
        part(curve(name+'_UpperLid',upper,.0017 if kind=='miriam' else .0012,'hair'))
        part(curve(name+'_LowerLid',lower,.0010,'innerskin'))
        brow=[(cx-w*.94,face_y(cx-w*.94),cz+.018),(cx,face_y(cx),cz+.024),(cx+w*.94,face_y(cx+w*.94),cz+.018)]
        part(tube(name+'_Brow',brow,[.0013,.0027,.0008],'hair',8,.55))
        if kind=='miriam':
            outer=cx+side*w
            part(curve(name+'_LashAccent',[(outer-side*.008,face_y(outer-side*.008),cz+.007),(outer,face_y(outer),cz+.004),(outer+side*.005,face_y(outer+side*.005),cz+.008)],.0011,'hair'))
    for side in (-1,1):part(uv(name+'_Nostril',(side*.009,-.094 if kind!='miriam' else -.090,1.607 if kind!='miriam' else 1.615),(.0023,.0008,.0012),'innerskin',16,8))
    # Restrained lip contour; separate highlights make the expression readable in cel light.
    mouthz=1.585 if kind=='miriam' else 1.578;mw=.024 if kind=='miriam' else .026
    part(curve(name+'_MouthLine',[(-mw,-.075,mouthz+.004),(-mw*.4,-.083,mouthz),(0,-.085,mouthz-.001),(mw*.4,-.083,mouthz),(mw,-.075,mouthz+.004)],.0008,'lip'))
    part(tube(name+'_UpperLip',[(-mw,-.075,mouthz+.004),(-mw*.3,-.084,mouthz+.003),(0,-.085,mouthz+.001),(mw*.3,-.084,mouthz+.003),(mw,-.075,mouthz+.004)],[.0005,.0017,.0015,.0017,.0005],'lip',8,.45))
    part(tube(name+'_LowerLip',[(-mw*.8,-.078,mouthz+.001),(0,-.085,mouthz-.003),(mw*.8,-.078,mouthz+.001)],[.0005,.0017,.0005],'miriamblush' if kind=='miriam' else 'innerskin',8,.45))
    # Scalp follows the cranial silhouette, with a shaped hairline and flowing locks.
    scalp=[]
    for j in range(15):
        t=j/14;row=[]
        for i in range(65):
            a=i*math.tau/64;front=max(0,math.cos(a));bottom=1.59+.116*front
            z=bottom+(1.779-bottom)*t
            rx=.101*math.sqrt(max(.001,1-((z-1.665)/.118)**2));ry=.099*math.sqrt(max(.001,1-((z-1.665)/.118)**2))
            row.append((math.sin(a)*rx,-math.cos(a)*ry+.002,z))
        scalp.append(row)
    part(patch(name+'_HairScalp',scalp,'hair'))
    # Curved locks are tapered strips with visible flow, not round clay clumps.
    for side in (-1,1):
        for i in range(3):
            points=[(side*(.008+i*.013),.004,1.779-i*.002),(side*(.035+i*.015),-.053+i*.012,1.760),(side*(.065+i*.012),-.075+i*.014,1.728),(side*(.090+i*.003),-.036+i*.018,1.675-i*.008)]
            part(tube(name+'_SweptHairLock',points,[.012,.019,.016,.002],'hairshine' if i==1 else 'hair',12,.40))
        if kind=='jesus':
            for i in range(5):
                x=side*(.087+i*.004);y=-.005+i*.022
                points=[(side*.075,y,1.73),(x,y+.002,1.66),(x+side*.008,y+.01,1.59),(x-side*.006,y+.002,1.52),(x+side*.008,y-.006,1.475)]
                part(tube(name+'_ShoulderLengthHair',points,[.009,.014,.017,.013,.001],'hairshine' if i==3 else 'hair',12,.6))
        elif kind=='traveler':
            for i in range(3):
                part(tube(name+'_ShortTempleHair',[(side*.078,.015+i*.023,1.73),(side*.096,.01+i*.023,1.68),(side*.09,.008+i*.023,1.625)],[.014,.018,.002],'hair',10,.55))
    if kind=='jesus':
        beardrows=[]
        for j in range(9):
            t=j/8;row=[]
            for i in range(33):
                u=-1+i/16;z=(1.608-.025*(1-abs(u)))*(1-t)+(1.535+.020*abs(u)**2)*t
                x=u*(.073*(1-t)+.052*t)
                facew,facefront=.08,.078
                for ra,rb in zip(headrings,headrings[1:]):
                    if ra[0]<=z<=rb[0]:
                        f=(z-ra[0])/(rb[0]-ra[0]);facew=ra[1]*(1-f)+rb[1]*f;facefront=ra[2]*(1-f)+rb[2]*f;break
                y=-facefront*math.sqrt(max(.025,1-(x/facew)**2))-.0015-.0005*math.cos(u*math.pi*5)
                row.append((x,y,z))
            beardrows.append(row)
        beard=patch(name+'_TailoredBeard',beardrows,'hair')
        for poly in beard.data.polygons:poly.flip()
        part(beard)
        for side in (-1,1):part(tube(name+'_Mustache',[(0,-.099,1.592),(side*.014,-.094,1.590),(side*.029,-.080,1.581)],[.0035,.005,.0015],'hair',10,.60))
        # Groomed beard follows the facial surface; no floating vertical rods.
    if kind=='miriam':
        # Open face veil: arch over hair plus softly folded back cloth, face never covered.
        rows=[]
        for j in range(14):
            t=j/13;row=[]
            for i in range(33):
                a=-math.pi/2+i*math.pi/32
                x=.112*math.sin(a);z=1.665+.125*math.cos(a)-.18*t
                y=-.015+.17*t+.006*math.cos(a*6)*t
                row.append((x*(1+.28*t),y,z))
            rows.append(row)
        part(cloth_surface(patch(name+'_LinenVeil',rows,'cloth')))
        rim=[(math.sin(-math.pi/2+i*math.pi/32)*.112,-.017,1.665+math.cos(-math.pi/2+i*math.pi/32)*.125) for i in range(33)]
        part(curve(name+'_VeilWovenEdge',rim,.003,'gold'))
        # Small gold ear hoops are clearly designed detail, not a historical claim.
        for side in (-1,1):
            ring=torus(name+'_EarHoop',(side*.097,-.006,1.603),.009,.0016,'gold',rotate=(math.pi/2,0,0));part(smooth(ring))
    # Sleeves, human hands with distinct fingers, fitted sandals.
    for side in (-1,1):
        suf='L' if side>0 else 'R';shoulder=(side*.14,0,1.39);elbow=(side*.285,-.003,1.205);wrist=(side*.327,-.026,1.025)
        pts=[shoulder,(side*.205,0,1.388),(side*.251,0,1.32),elbow,(side*.312,-.015,1.10),wrist]
        sleeve=tube(name+'_TailoredSleeve_'+suf,pts,[.045,.078,.072,.062,.057,.052],bodymat,32,.87)
        sleeves.append(sleeve)
        part(tube(name+'_Cuff_'+suf,[(side*.325,-.024,1.043),(side*.330,-.028,1.014)],[.054,.053],'gold' if kind!='jesus' else 'creamshadow',32,.87),'Forearm_'+suf)
        part(loft(name+'_Palm_'+suf,[(.944,.025,.019,.019,0),(.966,.031,.020,.020,0),(1.004,.028,.020,.020,0),(1.025,.022,.020,.020,0)],skinmat,24,center=(side*.332,-.029)),'Forearm_'+suf)
        for finger in range(4):
            xx=side*.332+(finger-1.5)*.014;length=[.037,.048,.046,.035][finger]
            part(tube(name+'_Finger_'+suf+str(finger),[(xx,-.032,.962),(xx,-.037,.937),(xx,-.046,.962-length)],[.008,.007,.0055],skinmat,8),'Forearm_'+suf)
        part(tube(name+'_Thumb_'+suf,[(side*.307,-.027,.991),(side*.296,-.038,.976),(side*.296,-.05,.958)],[.010,.009,.006],skinmat,10),'Forearm_'+suf)
        # Calves/trousers are only visible below hem; feet are a sculpted elongated form.
        if kind=='traveler':
            part(loft(name+'_LinenTrouser_'+suf,[(.14,.059,.060,.053,.02),(.22,.061,.061,.057,.03),(.46,.070,.070,.065,.02),(.72,.075,.075,.07,.01),(.86,.077,.077,.07,0)],'creamshadow',32,8,center=(side*.102,0)),'Leg_'+suf)
        part(tube(name+'_Ankle_'+suf,[(side*.10,0,.10),(side*.10,0,.24)],[.038,.043],skinmat,20),'Leg_'+suf)
        part(uv(name+'_Foot_'+suf,(side*.10,-.042,.076),(.045,.100,.041),skinmat,24,12),'Leg_'+suf)
        sole=bevelbox(name+'_SandalSole_'+suf,(side*.10,-.043,.017),(.105,.23,.034),'belt',.016);part(sole,'Leg_'+suf)
        for yy in (-.098,-.038):
            points=[(side*.10-.044,yy,.048),(side*.10-.032,yy,.089),(side*.10,yy,.109),(side*.10+.032,yy,.089),(side*.10+.044,yy,.048)]
            part(tube(name+'_SandalStrap_'+suf,points,[.007]*5,'belt',8,.4),'Leg_'+suf)
    # A fine voxel union provides a genuinely continuous sewn shoulder surface.
    # Exact Boolean was rejected after its coplanar/self-intersection edge cases
    # removed Miriam's garment in the actual preview. Reweight the final surface.
    pieces.remove(garment);garment=join_objects([garment]+sleeves,name+'_ContinuousTailoredGarment')
    mod=garment.modifiers.new('ContinuousWovenSurface','REMESH');mod.mode='VOXEL';mod.voxel_size=.005;mod.use_smooth_shade=True
    bpy.context.view_layer.objects.active=garment;bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=garment.modifiers.new('RelaxSewnShoulders','SMOOTH');mod.factor=.65;mod.iterations=4;bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=garment.modifiers.new('GarmentSurfaceBudget','DECIMATE');mod.ratio=.12;bpy.ops.object.modifier_apply(modifier=mod.name)
    assert len(garment.data.vertices)>1000 and min(v.co.z for v in garment.data.vertices)<skirt_bottom+.03, name+' missing garment surface'
    smooth(garment);garment.vertex_groups.clear();pieces.append(garment)
    armw=lambda p:max(0,min(1,(abs(p.x)-.17)/.085)) if p.z>.99 else 0
    fore=lambda p:max(0,min(1,(1.26-p.z)/.11))
    skin_weights(garment,{'Spine':lambda p:(1-armw(p))*blend(p),'Pelvis':lambda p:(1-armw(p))*(1-blend(p)),
        'UpperArm_L':lambda p:armw(p)*(1-fore(p)) if p.x>0 else 0,'Forearm_L':lambda p:armw(p)*fore(p) if p.x>0 else 0,
        'UpperArm_R':lambda p:armw(p)*(1-fore(p)) if p.x<0 else 0,'Forearm_R':lambda p:armw(p)*fore(p) if p.x<0 else 0})
    # Woven waist sash, narrow leather belt and tied end.
    waist=.166 if kind!='miriam' else .141
    part(loft(name+'_WaistSash',[(1.015,waist+.005,.111,.108,.01),(1.07,waist+.006,.113,.109,.01)],'gold' if kind=='miriam' else 'belt',64,14),'Pelvis')
    knotx=.095
    part(tube(name+'_SashKnot',[(knotx,-.108,1.05),(knotx+.012,-.127,1.035),(knotx+.018,-.121,1.017)],[.011,.016,.009],'gold' if kind=='miriam' else 'belt',12),'Pelvis')
    part(tube(name+'_SashEnd',[(knotx+.012,-.12,1.03),(knotx+.034,-.144,.94),(knotx+.024,-.145,.83)],[.013,.013,.009],'gold' if kind=='miriam' else 'belt',8,.28),'Pelvis')
    if kind=='jesus':
        # Broad mantle follows the shoulder and hangs in continuous cloth pleats.
        rows=[]
        for j in range(22):
            t=j/21;z=1.465-.94*t;row=[]
            for i in range(13):
                u=i/12;x=-.19+.145*u+.027*math.sin(t*3)
                y=-.082-.066*math.sin(min(1,t*2)*math.pi/2)-.008*math.cos(u*math.pi*6)
                row.append((x,y,z+.012*math.sin(u*math.pi)))
            rows.append(row)
        obj=cloth_surface(patch(name+'_CrimsonDrapeFront',rows,'red'),front=True);pieces.append(skin_weights(obj,{'Spine':blend,'Pelvis':lambda p:1-blend(p)}))
        rows=[]
        for j in range(20):
            t=j/19;row=[]
            for i in range(19):
                u=i/18;row.append((-.205+.36*u,.095+.065*t+.012*math.cos(u*math.pi*8),1.46-.94*t+.025*math.sin(u*math.pi)))
            rows.append(row)
        obj=cloth_surface(patch(name+'_CrimsonDrapeBack',rows,'red'));pieces.append(skin_weights(obj,{'Spine':blend,'Pelvis':lambda p:1-blend(p)}))
        rows=[]
        for j in range(13):
            t=j/12;y=-.10+.22*t;row=[]
            for i in range(9):u=i/8;row.append((-.21+.16*u,y,1.445+.030*math.sin(t*math.pi)+.004*math.cos(u*math.pi*4)))
            rows.append(row)
        part(cloth_surface(patch(name+'_MantleShoulder',rows,'red')),'Spine')
    elif kind=='miriam':
        rows=[]
        for j in range(16):
            t=j/15;row=[]
            for i in range(13):
                u=i/12;row.append((-.15+.105*u+.21*t,-.103-.024*math.sin(t*math.pi)-.004*math.cos(u*12),1.43-.37*t))
            rows.append(row)
        part(cloth_surface(patch(name+'_CreamShoulderWrap',rows,'cloth'),front=True),'Spine')
    else:
        # A shaped cross-body satchel, with an actual curved strap.
        part(bevelbox(name+'_TravelSatchel',(.19,.107,.93),(.16,.075,.19),'belt',.025),'Pelvis')
        part(bevelbox(name+'_SatchelFlap',(.19,.061,.97),(.16,.02,.092),'woodlight',.016),'Pelvis')
        part(curve(name+'_SatchelStrap',[(-.14,-.09,1.44),(-.08,-.126,1.32),(.04,-.132,1.14),(.18,-.105,.99)],.012,'belt'),'Spine')
    # Keep the established generic animation contract and weight transition at shoulders/waist.
    bpy.ops.object.armature_add();arm=bpy.context.object;arm.name=name+'_Rig';bpy.ops.object.mode_set(mode='EDIT')
    root=arm.data.edit_bones[0];root.name='Root';root.head=(0,0,0);root.tail=(0,0,.3)
    def bone(n,h,t,parent):b=arm.data.edit_bones.new(n);b.head=h;b.tail=t;b.parent=arm.data.edit_bones[parent]
    bone('Pelvis',(0,0,.85),(0,0,1.05),'Root');bone('Spine',(0,0,1.05),(0,0,1.48),'Pelvis');bone('Head',(0,0,1.48),(0,0,1.77),'Spine')
    for side in (-1,1):
        s='L' if side>0 else 'R';bone('UpperArm_'+s,(side*.19,0,1.43),(side*.285,-.003,1.205),'Spine');bone('Forearm_'+s,(side*.285,-.003,1.205),(side*.332,-.029,.965),'UpperArm_'+s);bone('Leg_'+s,(side*.10,0,.90),(side*.10,0,.10),'Pelvis')
    bpy.ops.object.mode_set(mode='OBJECT')
    body=join_objects(pieces,name+'_ToonSkinnedMesh');mod=body.modifiers.new('SmoothCharacterSkin','ARMATURE');mod.object=arm;body.parent=arm
    h=height/1.79;arm.scale=(h,h,h)
    scene.render.fps=30;scene.frame_start=1;scene.frame_end=121
    for frame,amount in [(1,0),(31,1),(61,0),(91,-1),(121,0)]:
        for n,rot in [('Spine',(.004*amount,0,.004*amount)),('Head',(.008*amount,0,-.018*amount)),('UpperArm_L',(0,.006*amount,.007*amount)),('UpperArm_R',(0,-.006*amount,-.007*amount))]:
            pb=arm.pose.bones[n];pb.rotation_mode='XYZ';pb.rotation_euler=rot;pb.keyframe_insert(data_path='rotation_euler',frame=frame)
    arm.animation_data.action.name=name+'_BasicIdle';scene.frame_set(1)
    export(POD/(name+'.fbx'),[arm,body],f'Original stylized adult {height:.2f}m; sculpted facial planes, almond eyes, tailored garment, Generic BasicIdle; not approved final character art',True)
    return scene,[arm,body]

def lathe(name,profile,mat,n=40):
    return loft(name,[(z,r,r,r,0) for z,r in profile],mat,n)

def improved_hut():
    cx,cy=-4,-3
    # Fine mortar joints and staggered masonry replace oversized toy blocks.
    for row in range(7):
        z=.17+row*.30
        for col in range(10):
            x=cx-2.18+col*.485+(row%2)*.05
            bevelbox('Hut_BackStone', (x,cy+1.68,z),(.469,.36,.286),random.choice(['stone','stone','stone_light']),.012)
        for side in (-1,1):
            for col in range(7):
                yy=cy-1.47+col*.48
                bevelbox('Hut_SideStone',(cx+side*2.4,yy,z),(.36,.464,.286),random.choice(['stone','stone_light']),.012)
        for side in (-1,1):
            for col in range(3):
                bevelbox('Hut_FrontStone',(cx+side*(1.34+col*.48),cy-1.70,z),(.464,.36,.286),random.choice(['stone','stone_light']),.012)
    bevelbox('Hut_DoorLintel',(cx,cy-1.70,2.21),(2.32,.45,.24),'stone_light',.03)
    for i in range(7):
        bevelbox('Hut_RoofTimber',(cx-2.3+i*.76,cy,2.40),(.12,4.04,.16),'wood',.025)
    for i in range(24):
        bevelbox('Hut_RoofSlat',(cx,cy-1.99+i*.172,2.50),(5.12,.158,.065),'woodlight',.014)
    # Rolled awning edge and a gently sagging woven roof surface.
    rows=[]
    for j in range(9):
        y=cy-2.12+j*.53;row=[]
        for i in range(15):x=cx-2.60+i*5.2/14;row.append((x,y,2.60-.030*math.sin(i*math.pi/14)*math.sin(j*math.pi/8)))
        rows.append(row)
    patch('Hut_WovenAwning',rows,'cloth')
    for y in (cy-2.12,cy+2.12):curve('Hut_AwningEdge',[(cx-2.6,y,2.60),(cx,y,2.60),(cx+2.6,y,2.60)],.025,'creamshadow')
    bevelbox('Hut_EntryStep',(cx,cy-2.0,.06),(2.4,.65,.13),'stone_light',.04)
    bevelbox('Hut_BenchSeat',(cx-1.5,cy+.8,.47),(1.5,.40,.10),'woodlight',.025)
    for x in (cx-2.05,cx-.95):bevelbox('Hut_BenchLeg',(x,cy+.8,.23),(.11,.32,.46),'wood',.012)
    for x,y,s in [(-6.4,-5.1,.8),(-5.95,-5.1,.55),(-2.2,-4.5,.85),(-5.1,-2.4,.55)]:
        o=lathe('TerracottaWaterJar',[(0,.14),(.08,.23),(.33,.27),(.51,.22),(.61,.10),(.68,.10),(.70,.12)],'terracotta');o.location=(x,y,.02);o.scale=(s,s,s)
        torus('JarRaisedLip',(x,y,.02+.70*s),.106*s,.013*s,'gold')
        for side in (-1,1):curve('JarHandle',[(x+side*.105*s,y,.57*s),(x+side*.23*s,y,.56*s),(x+side*.24*s,y,.43*s),(x+side*.19*s,y,.38*s)],.020*s,'terracotta')

def leaf_mesh(name,leaves):
    verts=[];faces=[]
    for center,direction,length,width,shade in leaves:
        p=Vector(center);d=Vector(direction).normalized();u=d.cross(Vector((0,0,1)))
        if u.length<.01:u=Vector((1,0,0))
        u.normalize();n=d.cross(u).normalized();base=len(verts)
        for along,across,height in [(0,0,0),(.27,-.7,0),(.62,-1,0),(1,0,0),(.62,1,0),(.27,.7,0),(.48,0,.08)]:verts.append(p+d*(along*length)+u*(across*width)+n*(height*length))
        for i in range(6):
            faces.append((base+i,base+(i+1)%6,base+6))
            faces.append((base+6,base+(i+1)%6,base+i))
    ob=mesh(name,verts,faces,None)
    for key in ('olive','olive_light','leafsilver'):ob.data.materials.append(M[key])
    for i,p in enumerate(ob.data.polygons):p.material_index=leaves[i//12][4]
    return ob

def improved_olive(x,y,s):
    tube('OliveSculptedTrunk',[(x,y,0),(x+.05*s,y,.55*s),(x+.11*s,y-.03*s,1.25*s),(x+.04*s,y,1.75*s)],[.16*s,.14*s,.10*s,.055*s],'wood',16)
    leaves=[]
    for branch in range(12):
        a=branch*math.tau/12+random.uniform(-.18,.18);z=random.uniform(1.45,1.9)*s
        spread=random.uniform(.75,1.20)*s
        tip=Vector((x+math.cos(a)*spread,y+math.sin(a)*spread,z+random.uniform(.3,.7)*s))
        start=Vector((x+.07*s,y,z-.6*s));mid=start.lerp(tip,.55)+Vector((0,0,.13*s))
        tube('OliveSculptedLimb',[start,mid,tip],[.07*s,.045*s,.009*s],'wood',10)
        for twig in range(8):
            t=.22+twig*.10;base=mid.lerp(tip,t);a2=a+(twig%2*2-1)*random.uniform(.5,1.3)
            end=base+Vector((math.cos(a2)*random.uniform(.32,.6)*s,math.sin(a2)*random.uniform(.32,.6)*s,random.uniform(-.20,.30)*s))
            tube('OliveTwig',[base,end],[.012*s,.002*s],'woodlight',6)
            for k in range(8):
                q=base.lerp(end,k/8)
                for side in (-1,1):
                    aa=a2+side*.95;direction=(math.cos(aa),math.sin(aa),random.uniform(.05,.35));leaves.append((q,direction,random.uniform(.15,.23)*s,.025*s,random.randrange(3)))
    leaf_mesh('OliveLanceolateLeaves',leaves)

def improved_boat(x,y,angle):
    parts=[];verts=[];faces=[]
    # Curved clinker hull with a separate inner surface, open top and a pointed prow.
    stations=[(-1.35,.20),(-1.05,.55),(-.55,.74),(.05,.80),(.65,.70),(1.15,.48),(1.65,.035)]
    for j,(yy,w) in enumerate(stations):
        for side in (-1,1):
            for level in range(5):
                t=level/4;verts.append((side*w*(.62+.38*t),yy,.12+.46*t+.09*abs(yy)/1.65))
    for j in range(len(stations)-1):
        for side in range(2):
            for level in range(4):a=j*10+side*5+level;faces.append((a,a+1,a+11,a+10))
        faces.append((j*10,j*10+5,(j+1)*10+5,(j+1)*10))
    ob=smooth(mesh('FishingBoat_CurvedHull',verts,faces,'woodlight'));sol=ob.modifiers.new('HullPlankThickness','SOLIDIFY');sol.thickness=.045;bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=sol.name);parts.append(ob)
    for side in (-1,1):
        for level in (.36,.59):parts.append(curve('BoatClinkerSeam',[(side*w*(.62+.38*((level-.12)/.46)),yy,level+.09*abs(yy)/1.65) for yy,w in stations],.018,'wood'))
    for yy in (-.65,.05,.72):parts.append(bevelbox('BoatSeat',(0,yy,.44),(1.20 if yy<.5 else 1.0,.20,.065),'woodlight',.02))
    parts.append(curve('BoatOar',[(-.85,-1.2,.64),(0,.15,.71),(.65,1.25,.72)],.027,'woodlight'))
    parts.append(bevelbox('BoatOarBlade',(.75,1.42,.72),(.15,.45,.035),'woodlight',.025))
    boat=join_objects(parts,'FishingBoat_Detailed');boat.location=(x,y,.02);boat.rotation_euler.z=angle

def environment():
    scene=new_scene('Galilee_ToonEnvironment')
    land=cone('Shore_Land',(0,0,-.20),12,11.8,.42,'sand',64);land.scale=(1,.78,1)
    for v in land.data.vertices:v.co.x=min(v.co.x,4.7)
    bevelbox('Galilee_Water',(14,0,-.055),(19,32,.06),'water',.015)
    for i in range(30):
        yy=-9+i*.61;xx=4.67+.10*math.sin(i*.8)
        o=uv('ShoreWeatheredStone',(xx,yy,.035),(random.uniform(.22,.47),random.uniform(.20,.40),random.uniform(.10,.22)),random.choice(['stone','stone_light']),12,8)
        o.rotation_euler=(random.uniform(-.2,.2),random.uniform(-.2,.2),random.random()*math.pi)
        curve('WaterShoreRipple',[(4.93,yy,.002),(5.25,yy+.06,.002),(5.65,yy+.04,.002)],.008,'foam')
    for i in range(30):
        xx=random.uniform(6,20);yy=random.uniform(-14,14);length=random.uniform(.3,1.4)
        curve('WaterDistantRipple',[(xx,yy,-.009),(xx+length*.5,yy+.025,-.009),(xx+length,yy,-.009)],.008,'foam')
    improved_hut()
    for x,y,s in [(-8,-3,1.25),(-7,3,1.4),(-3,5,1.15),(1,5.5,1.05)]:improved_olive(x,y,s)
    improved_boat(3.1,-3,-.3);improved_boat(6.3,3,.25)
    # Sparse groundcover stays away from the central authored walking area.
    for i in range(55):
        x,y=(random.uniform(3.95,4.65),random.uniform(-8,8)) if i<35 else (random.uniform(-9,-7),random.uniform(-6,6))
        for j in range(4):
            a=j*math.tau/4+random.random();h=random.uniform(.15,.38)
            curve('ShoreSedge',[(x,y,.01),(x+.04*math.cos(a),y+.04*math.sin(a),h*.65),(x+.14*math.cos(a),y+.14*math.sin(a),h)],.007,'grass')
    for x in (-.7,1.0):tube('NetRack_Post',[(x,2.6,0),(x+.025,2.6,1.62)],[.045,.034],'wood',12)
    tube('NetRack_Top',[(-.83,2.6,1.62),(1.15,2.6,1.60)],[.042,.036],'wood',12)
    for i in range(13):
        xx=-.65+i*.135;curve('FishingNet_DrapeVertical',[(xx,2.60,1.5),(xx+.02,2.66,.95),(xx+.03,2.64,.43+.08*math.sin(i*.3))],.0045,'belt')
    for i in range(9):
        zz=.48+i*.125;curve('FishingNet_DrapeHorizontal',[(-.65,2.61,zz),(.1,2.67,zz-.08),(.98,2.61,zz)],.0045,'belt')
    for x,y in [(-.8,-.9),(1,-1.4),(0,.3),(-2,-3.5),(-3,-4.8)]:
        o=uv('Walkway_WornSlab',(x,y,.018),(.52,.35,.045),'stone_light',12,8);o.rotation_euler.z=random.random()
    return scene

def copy_character(objects,scene,loc):
    copies={}
    for obj in objects:copy=obj.copy();copies[obj]=copy;scene.collection.objects.link(copy)
    for original,copy in copies.items():
        if original.parent in copies:copy.parent=copies[original.parent]
        for mod in copy.modifiers:
            if mod.type=='ARMATURE' and mod.object in copies:mod.object=copies[mod.object]
    copies[objects[0]].location=loc

def consolidate_environment(scene):
    # Preserve separate visual groups for culling/replacement while avoiding hundreds
    # of tiny mesh renderers. Unity gameplay anchors are independent of these names.
    groups={}
    for obj in list(scene.objects):
        if obj.type!='MESH':continue
        name=obj.name
        if name.startswith(('Hut_','Terracotta','Jar')):group='Galilee_ShelterMasonryAndProps'
        elif name.startswith('Olive'):group='Galilee_OliveGrove'
        elif name.startswith(('Galilee_Water','Water')):group='Galilee_WaterAndRipples'
        elif name.startswith('ShoreSedge'):group='Galilee_ShoreGroundcover'
        elif name.startswith(('NetRack','FishingNet')):group='Galilee_DrapedFishingNet'
        elif name.startswith('FishingBoat'):continue
        elif name.startswith('Walkway'):group='Galilee_WornPath'
        else:group='Galilee_LandAndShoreRocks'
        groups.setdefault(group,[]).append(obj)
    for group,objects in groups.items():join_objects(objects,group)

def studio(scene,pos,target,size):
    camera_light(scene,pos,target,size)
    scene.render.resolution_x=1600;scene.render.resolution_y=1200;scene.cycles.samples=48
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.45,.53,.61,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
    # A large white key preserves authored palette; the Unity toon shader is separate.
    for o in scene.objects:
        if o.type=='LIGHT' and o.data.type=='AREA':o.data.color=(1,.90,.77);o.data.energy=1100;o.data.size=5

chars=[]
for name,height,kind in [('JesusProxy',1.80,'jesus'),('MiriamProxy',1.70,'miriam'),('TravelerProxy',1.76,'traveler')]:chars.append(character(name,height,kind))
shore=environment();consolidate_environment(shore);export(POD/'GalileeShore.fbx',[o for o in shore.objects if o.type=='MESH'],'Refined original Galilee shore, unchanged runtime anchors; raw Unity hut (+4,+3), environment Y180 => hut(-4,-3), door-Z, water+X.')
for (_,objects),loc in zip(chars,[(.1,-1.1,0),(1.2,.4,0),(-1.3,-2.4,0)]):copy_character(objects,shore,loc)
studio(shore,(13,-19,13),(-1,-1,.8),25)
lineup=new_scene('Dawn_ToonCharacterLineup')
bevelbox('StudioFloor',(0,0,-.065),(5.0,3,.12),'stone_light',.05)
for (_,objects),loc in zip(chars,[(-1.05,0,0),(0,0,0),(1.05,0,0)]):copy_character(objects,lineup,loc)
studio(lineup,(2.5,-10,3.3),(0,0,.95),4.2)
portrait=new_scene('Dawn_ToonFaces')
for (_,objects),loc in zip(chars,[(-.34,0,0),(0,0,0),(.34,0,0)]):copy_character(objects,portrait,loc)
studio(portrait,(.1,-8,2.35),(0,0,1.60),1.2)
portrait.render.resolution_x=1800;portrait.render.resolution_y=1000
MANIFEST['material_colors']={mat.name:list(mat.diffuse_color)[:3] for mat in bpy.data.materials if mat.name.startswith(('POD_','FC_'))}
MANIFEST['render_notice']='Actual Blender source render, not Unity gameplay. Unity toon shading is configured separately.'
MANIFEST['known_limits']=['Procedural authored topology, not scan or approved likeness','Generic BasicIdle only; no authored walk cycle, finger rig or facial animation','No UV textures/normal maps yet; deliberate multi-material stylized surfaces','Character eye/lip/hair details and continuous garments are real geometry; Unity material import must preserve all POD_* material slots']
(OUT/'AssetManifest.json').write_text(json.dumps(MANIFEST,ensure_ascii=False,indent=2)+'\n')
(OUT/'ToonDawnManifest.json').write_text(json.dumps(MANIFEST,ensure_ascii=False,indent=2)+'\n')
bpy.context.window.scene=lineup
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/ToonDawnCharactersAndShore.blend'),compress=True)
if args.render:
    for scene,filename in [(lineup,'ToonDawn_CharacterLineup.png'),(portrait,'ToonDawn_FaceDetail.png'),(shore,'ToonDawn_GalileeShore.png')]:
        bpy.context.window.scene=scene;scene.render.filepath=str(OUT/'Previews'/filename);bpy.ops.render.render(write_still=True)
print('TOON_DAWN_ART_GENERATION_OK '+json.dumps({'assets':len(MANIFEST['assets']),'revision':MANIFEST['revision']}))
