import json,sys
d=json.load(open('ring-sim-result.json',encoding='utf-8'))
for r in d:
    s=r['spawner']
    print('==',r['name'],'cfgOk',r['cfgOk'],'wall',r['wallOk'],'invalid',r['invalid'],'uniq',r['uniqueLayouts'],'retry',r['retriedBuilds'],'maxAtt',r['maxAttempts'],'isl',r['islandCount'].get('median'),'gapmin',round(r['coastGapRealM'].get('min',0),1),'blk%',round(r['blockedStraightLegPct'],1))
    print('   dock',' | '.join('%s %d/%d/%d t%.1f'%(z[:3],v['min'],v['median'],v['max'],r['modelCruiseSecByZone'][z]['median']) for z,v in r['dockFromLighthouseByZone'].items()),'far',round(r['farthestDockFromLighthouseM'].get('median',0)))
    print('   spawn ok',s['ok'],'fail',s['fail'],s['errors'][:1],'cnt',s['count'].get('min'),s['count'].get('median'),s['count'].get('max'),'byZone',s['pointsByZone'],'gapIsl',round(s['gapToIslandCoastM'].get('min',0),1),round(s['gapToIslandCoastM'].get('median',0),1),'<10m',s['nearIslandUnder10m'],'/',s['total'])
