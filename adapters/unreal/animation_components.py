"""Transform AnimationClip -> native LevelSequence tracks, with editable channel extraction."""
import copy,hashlib,json,math
import unreal
KEY="EngineMigratorAnimation"

def helper(actor):return bool(unreal.EditorAssetLibrary.get_metadata_tag(actor,KEY))
def axis(property):
    prefix,component=property.split(".");index={"z":0,"x":1,"y":2}[component]
    if prefix=="m_LocalPosition":return index,100.0
    if prefix=="m_LocalScale":return 6+index,1.0
    raise RuntimeError("Unsupported animation channel "+property)

def import_all(package,actors,subsystem,base):
    result=[];tools=unreal.AssetToolsHelpers.get_asset_tools()
    for node in package["nodes"]:
        if not node.get("hasAnimation"):continue
        animation=node["animation"]
        for clip in animation["clips"]:
            asset_name="A_"+hashlib.sha256((node["id"]+":"+clip["id"]).encode()).hexdigest()[:20]
            asset=tools.create_asset(asset_name,base+"/Animations",unreal.LevelSequence,unreal.LevelSequenceFactoryNew())
            asset.set_display_rate(unreal.FrameRate(round(clip["frameRate"]),1));asset.set_tick_resolution_directly(unreal.FrameRate(24000,1))
            asset.set_playback_start(0);asset.set_playback_end(round(clip["length"]*clip["frameRate"]))
            for target in sorted(set(c["target"] for c in clip["curves"])):
                actor=actors[target];binding=asset.add_possessable(actor);binding.set_name(target)
                track=binding.add_track(unreal.MovieScene3DTransformTrack);section=track.add_section();section.set_range(0,round(clip["length"]*clip["frameRate"])+1)
                channels=section.get_all_channels();root=actor.root_component;p=root.get_editor_property("relative_location");r=root.get_editor_property("relative_rotation");s=root.get_editor_property("relative_scale3d")
                for channel,value in zip(channels,[p.x,p.y,p.z,r.roll,r.pitch,r.yaw,s.x,s.y,s.z]):channel.set_default(value)
                # Legacy Animation binds a whole TRS property group; unkeyed axes use its default value.
                properties=[c["property"] for c in clip["curves"] if c["target"]==target]
                if any(p.startswith("m_LocalPosition.") for p in properties):
                    for channel in channels[:3]:channel.set_default(0)
                if any(p.startswith("m_LocalScale.") for p in properties):
                    for channel in channels[6:9]:channel.set_default(1)
                for curve in (c for c in clip["curves"] if c["target"]==target):
                    index,mult=axis(curve["property"]);channel=channels[index]
                    for source in curve["keys"]:
                        key=channel.add_key(unreal.FrameNumber(round(source["time"]*24000)),source["value"]*mult,time_unit=unreal.MovieSceneTimeUnit.TICK_RESOLUTION)
                        key.set_interpolation_mode({"linear":unreal.RichCurveInterpMode.RCIM_LINEAR,"constant":unreal.RichCurveInterpMode.RCIM_CONSTANT,"cubic":unreal.RichCurveInterpMode.RCIM_CUBIC}[source["mode"]])
                        if source["mode"]=="cubic":key.set_tangent_mode(unreal.RichCurveTangentMode.RCTM_BREAK);key.set_arrive_tangent(source["inTangent"]*mult/24000);key.set_leave_tangent(source["outTangent"]*mult/24000)
            if not unreal.EditorAssetLibrary.save_loaded_asset(asset):raise RuntimeError("Sequence save failed")
            presenter=unreal.EMBlueprintLibrary.create_animation_presenter(base+"/Animations/"+asset_name+"_Presenter")
            if presenter.startswith("ERROR:"):raise RuntimeError(presenter)
            if node.get("runtimeTemplate"):
                # Cooked Blueprint tick registration may differ from the editor.
                # A template has no auto-play event graph; clones use their clock.
                blueprint=unreal.load_asset(base+"/Animations/"+asset_name+"_Presenter")
                graph=unreal.BlueprintEditorLibrary.find_event_graph(blueprint)
                if graph:unreal.BlueprintEditorLibrary.remove_graph(blueprint,graph)
                if not unreal.BlueprintEditorLibrary.compile_blueprint(blueprint) or not unreal.EditorAssetLibrary.save_loaded_asset(blueprint):raise RuntimeError("Dormant animation presenter compile/save failed")
            actor=subsystem.spawn_actor_from_class(unreal.load_class(None,presenter),unreal.Vector());actor.set_sequence(asset)
            settings=actor.get_editor_property("playback_settings");settings.set_editor_property("auto_play",False);settings.set_editor_property("loop_count",unreal.MovieSceneSequenceLoopCount(-1 if clip["wrap"]=="Loop" else 0));settings.set_editor_property("pause_at_end",clip["wrap"]=="ClampForever");actor.set_editor_property("playback_settings",settings)
            actor.set_actor_tick_enabled(animation["enabled"] and animation["autoPlay"] and clip["id"]==animation["defaultClip"])
            actor.set_actor_label("Animation "+node["name"]+" / "+clip["name"]);actor.tags=[unreal.Name("EM_Animation"),unreal.Name(node["id"]),unreal.Name(clip["id"])];unreal.EditorAssetLibrary.set_metadata_tag(actor,KEY,json.dumps({"owner":node["id"],"clip":clip}));result.append(actor)
            actor.set_owner(actors[node["id"]])
            actor.tags=list(actor.tags)+[unreal.Name("EMRuntimeAnimation"),unreal.Name("EMAnimationLength:"+str(clip["length"])),unreal.Name("EMAnimationWrap:"+clip["wrap"])]+([unreal.Name("EMAnimationAutoPlay")] if animation["enabled"] and animation["autoPlay"] and clip["id"]==animation["defaultClip"] else [])+([unreal.Name("EMRuntimeTemplate")] if node.get("runtimeTemplate") else [])
            actor.tags=list(actor.tags)+[unreal.Name("EMAnimationBinding:"+unreal.GuidLibrary.conv_guid_to_string(binding.get_id())+":"+binding.get_name()) for binding in asset.get_bindings()]
            if node.get("runtimeTemplate"):
                for binding in asset.get_bindings():actor.set_binding(asset.get_binding_id(binding),[actor],False)
    return result

def persist(package,actors,nodes):
    source_nodes={n["id"]:n for n in package["nodes"]}
    for actor in actors:
        owner=str(actor.tags[1]);clip=next(c for c in source_nodes[owner]["animation"]["clips"] if c["id"]==str(actor.tags[2]))
        unreal.EditorAssetLibrary.set_metadata_tag(actor,KEY,json.dumps({"owner":owner,"clip":clip}))
        sequence=actor.get_sequence()
        for binding in sequence.get_bindings():
            if not unreal.EMBlueprintLibrary.bind_animation(sequence,binding.get_id(),nodes[binding.get_name()]):raise RuntimeError("Saved-map animation rebinding failed")
        if not unreal.EditorAssetLibrary.save_loaded_asset(sequence):raise RuntimeError("Rebound sequence save failed")

def export_all(actors,node_ids,package):
    by_id={n["id"]:n for n in package["nodes"]}
    for actor in actors:
        metadata=unreal.EditorAssetLibrary.get_metadata_tag(actor,KEY)
        if not metadata:continue
        data=json.loads(metadata);owner=by_id[data["owner"]];original=next((c for c in owner.get("animation",{}).get("clips",[]) if c["id"]==data["clip"]["id"]),None)
        if original is None:raise RuntimeError("Missing animation owner/clip")
        sequence=actor.get_sequence();settings=actor.get_editor_property("playback_settings");original["wrap"]="Loop" if settings.get_editor_property("loop_count").get_editor_property("value")==-1 else "ClampForever" if settings.get_editor_property("pause_at_end") else original["wrap"] if original["wrap"] in ("Default","Once","Clamp") else "Once"
        original["frameRate"]=sequence.get_display_rate().numerator/sequence.get_display_rate().denominator
        resolution=sequence.get_tick_resolution()
        if resolution.numerator!=24000 or resolution.denominator!=1:raise RuntimeError("Edited sequence tick resolution is unsupported")
        expected_targets=set(c["target"] for c in original["curves"])
        if len(sequence.get_bindings())!=len(expected_targets) or set(b.get_name() for b in sequence.get_bindings())!=expected_targets:raise RuntimeError("Added/removed animation bindings are unsupported")
        if sequence.get_tracks():raise RuntimeError("Added sequence master tracks are unsupported")
        if sequence.get_playback_start()!=0 or sequence.get_playback_end()!=round(original["length"]*original["frameRate"]):raise RuntimeError("Edited animation playback range is unsupported")
        for binding in sequence.get_bindings():
            tracks=binding.get_tracks()
            if len(tracks)!=1 or not isinstance(tracks[0],unreal.MovieScene3DTransformTrack) or len(tracks[0].get_sections())!=1:raise RuntimeError("Unsupported edited animation track structure")
            supported=set(axis(c["property"])[0] for c in original["curves"] if c["target"]==binding.get_name())
            for index,channel in enumerate(tracks[0].get_sections()[0].get_all_channels()):
                if index not in supported and channel.get_keys():raise RuntimeError("Added animation channels are unsupported")
        for curve in original["curves"]:
            binding=next((b for b in sequence.get_bindings() if b.get_name()==curve["target"]),None)
            if binding is None:raise RuntimeError("Animation target binding missing")
            bound=sequence.locate_bound_objects(binding,actor.get_world())
            if len(bound)!=1 or node_ids.get(bound[0])!=curve["target"]:raise RuntimeError("Animation target reference changed/unsupported")
            tracks=binding.get_tracks()
            if len(tracks)!=1 or not isinstance(tracks[0],unreal.MovieScene3DTransformTrack) or len(tracks[0].get_sections())!=1:raise RuntimeError("Unsupported edited animation track structure")
            channel=tracks[0].get_sections()[0].get_all_channels()[axis(curve["property"])[0]];mult=axis(curve["property"])[1]
            keys=[]
            for key in channel.get_keys():
                mode=key.get_interpolation_mode();t=key.get_time(time_unit=unreal.MovieSceneTimeUnit.TICK_RESOLUTION).frame_number.value/24000
                keys.append({"time":t,"value":key.get_value()/mult,"inTangent":key.get_arrive_tangent()*24000/mult,"outTangent":key.get_leave_tangent()*24000/mult,"mode":"linear" if mode==unreal.RichCurveInterpMode.RCIM_LINEAR else "constant" if mode==unreal.RichCurveInterpMode.RCIM_CONSTANT else "cubic"})
            curve["keys"]=keys

def verify_all(actors,node_ids,package):
    restored=copy.deepcopy(package);export_all(actors,node_ids,restored)
    def equal(a,b):
        if isinstance(a,dict):return all(k in b and equal(v,b[k]) for k,v in a.items())
        if isinstance(a,list):return len(a)==len(b) and all(equal(x,y) for x,y in zip(a,b))
        if isinstance(a,(int,float)) and not isinstance(a,bool):return math.isclose(a,b,abs_tol=2e-6,rel_tol=1e-6)
        return a==b
    expected=[n["animation"] for n in package["nodes"] if n.get("hasAnimation")];actual=[n["animation"] for n in restored["nodes"] if n.get("hasAnimation")]
    if not equal(expected,actual):raise RuntimeError("Animation native channel/reference mismatch after reload: "+json.dumps(actual))
