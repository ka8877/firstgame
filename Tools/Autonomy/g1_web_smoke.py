#!/usr/bin/env python3
"""Exact G1 revision adapter for the unchanged real-input JourneySmoke driver."""
from pathlib import Path
import hashlib, importlib.util, json, shutil, sys, time
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/Validation'))
spec=importlib.util.spec_from_file_location('g1_existing_nativity_driver',ROOT/'Tools/Validation/nativity-web-smoke.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.QUEST_REVISIONS={'FantasyCards':'fm-nativity-2026-11-02','PathOfDawn':'gospel-encounter-2026-11-03'}
m.CURRENT_GUARDED_REVISIONS=(*m.CURRENT_GUARDED_REVISIONS,*m.QUEST_REVISIONS.values())
Original=m.JourneySmoke
class G1Smoke(Original):
 def __init__(self,*args,**kwargs):
  super().__init__(*args,**kwargs)
  source=Path(__file__);target=self.output/'RecordedHarness/Tools/Autonomy/g1_web_smoke.py';target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
  self.report['harness']['g1Wrapper']={'file':str(source.relative_to(ROOT)),'preservedFile':str(target.relative_to(ROOT)),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()}
  self.report['releaseProfile']='autonomy-g1';self.persist()
 def loaded(self,reload=False):
  started=time.monotonic()
  super().loaded(reload=reload)
  value=self.page.evaluate('''() => ({
   heap:performance.memory ? {usedJSHeapSize:performance.memory.usedJSHeapSize,totalJSHeapSize:performance.memory.totalJSHeapSize,jsHeapSizeLimit:performance.memory.jsHeapSizeLimit}:null,
   resourceCount:performance.getEntriesByType('resource').length,
   observedBrowserMilliseconds:performance.now()
  })''')
  value.update(navigation=self.navigation,readyAndSettledSeconds=time.monotonic()-started,
   scope='Observed cloud software-browser loader and settling time; JS heap is not Unity/GPU memory or physical-device performance.')
  self.report.setdefault('webMeasurements',[]).append(value);self.persist()
m.JourneySmoke=G1Smoke
if __name__=='__main__':
 if '--release-profile' not in sys.argv or sys.argv[sys.argv.index('--release-profile')+1]!='nativity':raise SystemExit('Explicit --release-profile nativity required for inherited strict driver; wrapper records autonomy-g1')
 if '--game' not in sys.argv or sys.argv[sys.argv.index('--game')+1]!='PathOfDawn':raise SystemExit('Only changed Gospel is re-executed in G1')
 raise SystemExit(m.main())
