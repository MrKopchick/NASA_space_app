from pathlib import Path
import json, re, sys

ROOT = Path(__file__).resolve().parent
# Can be run from anywhere: supply repo root as an argument, or place script in repo root.
repo = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT
art = repo / 'Assets/_Project/Art'
data = repo / 'Assets/_Project/Resources/Data'
if not art.is_dir() or not data.is_dir():
    sys.exit('ERROR: Repository root not found. Run: python s2_09_prepare.py "C:\\path\\to\\NASA_space_app"')

folders = ['Spacecraft', 'Planets', 'Icons', 'Portraits']
updated = 0
for sub in folders:
    for png in (art / sub).rglob('*.png'):
        meta = png.with_name(png.name + '.meta')
        if not meta.is_file():
            print('WARNING missing .meta:', meta.relative_to(repo))
            continue
        txt = meta.read_text(encoding='utf-8-sig')
        original = txt
        # Change only fields of existing Unity TextureImporter records, preserving GUIDs and other metadata.
        fields = {'filterMode': '0', 'textureType': '8', 'spriteMode': '1', 'enableMipMap': '0', 'textureCompression': '0', 'crunchedCompression': '0'}
        for key, value in fields.items():
            pattern = rf'(?m)^(\s*{re.escape(key)}:\s*)\d+(\s*)$'
            if re.search(pattern, txt):
                txt = re.sub(pattern, lambda m: m.group(1)+value+m.group(2), txt)
            else:
                print('WARNING field absent:', key, meta.relative_to(repo))
        if txt != original:
            meta.write_text(txt, encoding='utf-8', newline='\n')
            updated += 1
print('Updated .meta files:', updated)

files = {'components.json':'components','rockets.json':'rockets','orbits.json':'orbits','missions.json':'missions','events.json':'events'}
objects = {}
ids = set()
for filename, key in files.items():
    p = data / filename
    content = json.loads(p.read_text(encoding='utf-8-sig'))
    rows = content[key]
    assert isinstance(rows,list), f'{filename}: {key} must be array'
    objects[key] = rows
    row_ids = [x['Id'] for x in rows]
    assert len(set(row_ids)) == len(row_ids), f'{filename}: duplicate IDs'
    print(f'OK {filename}: {len(rows)} entries')
    ids.update(row_ids)

# Paths relative to Assets/_Project/Art; mappings are visual choices to confirm with UI team.
mapping = {
    'bus_cubesat_3u':'Spacecraft/cubesat.png',
    'bus_smallsat':'Spacecraft/satellite.png',
    'bus_deepspace':'Spacecraft/deep_space_probe.png',
    'pwr_solar_mini':'Icons/solar_panel.png',
    'pwr_solar_deployable':'Icons/solar_panel.png',
    'pwr_solar_wings':'Icons/solar_panel.png',
    'pwr_rtg':'Icons/power_core.png',
    'bat_li_small':'Icons/energy_cell.png',
    'bat_li_medium':'Icons/energy_cell.png',
    'bat_solid_heavy':'Icons/energy_cell.png',
    'comms_uhf_basic':'Icons/dish_antenna.png',
    'comms_sband':'Icons/dish_antenna.png',
    'comms_xband':'Icons/dish_antenna.png',
    'comms_laser':'Icons/space_telescope.png',
    'sci_camera_basic':'Icons/space_telescope.png',
    'sci_radar_sar':'Icons/space_telescope.png',
    'sci_spectrometer':'Icons/space_telescope.png',
    'sci_magnetometer':'Icons/space_telescope.png',
    'rocket_light':'Spacecraft/small_rocket.png',
    'rocket_medium':'Spacecraft/reusable_rocket.png',
    'rocket_heavy':'Spacecraft/heavy_lift_rocket.png',
    'orbit_leo':'Planets/earth.png',
    'orbit_lunar':'Planets/moon.png',
    'orbit_mars':'Planets/mars.png',
    'orbit_venus':'Planets/venus_planet.png',
}
relevant_ids = {r['Id'] for t in ['components','rockets','orbits'] for r in objects[t]}
assert relevant_ids == set(mapping), f'Mapping mismatch: missing={relevant_ids - set(mapping)}, extra={set(mapping)-relevant_ids}'
for item_id, path in mapping.items():
    assert (art / path).is_file(), f'Missing sprite for {item_id}: {path}'

# Explicitly separate visual mapping from game simulation data. Unity team must choose an asset-loading strategy.
out = data / 'sprite_mapping.json'
out.write_text(json.dumps({'sprites': mapping},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Created:', out.relative_to(repo), 'with',len(mapping),'IDs')
print('Done. Confirm choices and Unity importer settings in Editor before merging PR.')
