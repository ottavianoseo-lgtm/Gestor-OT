// Visibilidad y estilo de las capas del mapa.
//
// Se escribio para un refactor que no se podia mirar: mapInterop decidia color y
// visibilidad en cuatro lugares distintos con constantes propias, y se centralizo en
// applyLayerVisibility/applyLotStyle. Estos casos son la tabla de verdad del
// comportamiento ANTERIOR, mas lo que el refactor habilita (filtro y hover).
//
// Stubea lo justo de Leaflet, asi que no necesita navegador. NO corre con `dotnet test`:
//
//     node src/GestorOT.Tests/Js/mapInterop.visibility.test.js src/GestorOT.Api/wwwroot/js/mapInterop.js
//
const fs = require('fs');

const src = fs.readFileSync(process.argv[2], 'utf8');

let fails = 0;
function check(name, actual, expected) {
    const a = JSON.stringify(actual), e = JSON.stringify(expected);
    if (a !== e) { console.log(`  FALLA  ${name}\n         esperado ${e}\n         obtuvo   ${a}`); fails++; }
    else console.log(`  ok     ${name}`);
}

function makeLayer() {
    return {
        options: { weight: 2, fillOpacity: 0.35 },
        _handlers: {},
        setStyle(s) { Object.assign(this.options, s); },
        bindTooltip() { return this; },
        bindPopup() { return this; },
        on(ev, fn) { this._handlers[ev] = fn; return this; },
        fire(ev) { if (this._handlers[ev]) this._handlers[ev](); },
        addTo(map) { map._layers.add(this); return this; },
        getBounds() { return { isValid: () => true }; }
    };
}

const window = {};
global.window = window;
global.L = {
    polygon: () => makeLayer(),
    featureGroup: (ls) => ({ getBounds: () => ({ isValid: () => true }) })
};

eval(src);
const m = window.mapInterop;

function reset() {
    m.map = {
        _layers: new Set(),
        hasLayer(l) { return this._layers.has(l); },
        removeLayer(l) { this._layers.delete(l); },
        fitBounds() {},
        getContainer() { return null; },
        getZoom() { return 14; }
    };
    m.lotLayers = {}; m.fieldLayers = {};
    m.selectedLayer = null; m.selectedFieldId = null;
    m.lotFilter = null; m.viewMode = 'interactive';
    m.dotNetRef = null;
}

const COORDS = '[[[0,0],[1,0],[1,1],[0,0]]]';
function addLot(id, fieldId, status) {
    m.addLotPolygon(id, 'Lote ' + id, status || 'Active', 10, 'Campo', COORDS, fieldId);
}
function addField(id) {
    m.addFieldPolygon(id, 'Campo ' + id, 2, 100, COORDS);
}
function visibles() {
    return Object.keys(m.lotLayers).filter(id => m.map.hasLayer(m.lotLayers[id])).sort();
}
function fieldsVisibles() {
    return Object.keys(m.fieldLayers).filter(id => m.map.hasLayer(m.fieldLayers[id])).sort();
}
function seed() {
    reset();
    addField('A'); addField('B');
    addLot('a1', 'A'); addLot('a2', 'A'); addLot('b1', 'B');
}

console.log('\n--- visibilidad de lotes (comportamiento previo) ---');

seed();
check('interactive sin campo: ningun lote', visibles(), []);
check('interactive sin campo: campos visibles', fieldsVisibles(), ['A', 'B']);

seed(); m.selectField('A');
check('interactive + campo A: solo lotes de A', visibles(), ['a1', 'a2']);

seed(); m.setGisViewMode('lots');
check('modo lotes: todos', visibles(), ['a1', 'a2', 'b1']);
check('modo lotes: ningun campo', fieldsVisibles(), []);

seed(); m.setGisViewMode('fields');
check('modo campos: ningun lote', visibles(), []);
check('modo campos: campos visibles', fieldsVisibles(), ['A', 'B']);

seed(); m.setGisViewMode('fields'); m.selectField('B');
check('modo campos + campo B: lotes de B', visibles(), ['b1']);

seed(); m.setGisViewMode('lots'); m.selectField('A');
check('modo lotes + campo A: siguen todos', visibles(), ['a1', 'a2', 'b1']);

console.log('\n--- OT-38: el filtro y el modo de vista no se pisan ---');

seed(); m.setGisViewMode('lots');
check('filtro deja solo a1', (m.filterLots(['a1']), visibles()), ['a1']);
m.setGisViewMode('interactive');
check('cambiar de modo respeta el filtro', visibles(), []);
m.setGisViewMode('lots');
check('volver a lotes respeta el filtro', visibles(), ['a1']);
m.selectField('A');
check('seleccionar campo respeta el filtro', visibles(), ['a1']);
check('limpiar filtro devuelve todo', (m.filterLots(null), visibles()), ['a1', 'a2', 'b1']);

console.log('\n--- OT-36: el hover no pisa seleccion ni campo activo ---');

seed(); m.setGisViewMode('lots'); m.highlightLot('a1');
const sel = m.lotLayers['a1'];
check('lote seleccionado', [sel.options.weight, sel.options.fillOpacity], [4, 0.65]);
sel.fire('mouseover');
check('hover sobre el seleccionado suma', [sel.options.weight, sel.options.fillOpacity], [6, 0.8]);
sel.fire('mouseout');
check('al salir vuelve a SELECCIONADO, no al base', [sel.options.weight, sel.options.fillOpacity], [4, 0.65]);

seed(); m.selectField('A');
const enCampo = m.lotLayers['a2'];
check('lote de campo activo', [enCampo.options.weight, enCampo.options.fillOpacity], [2.5, 0.55]);
enCampo.fire('mouseover'); enCampo.fire('mouseout');
check('al salir vuelve al estilo de campo activo', [enCampo.options.weight, enCampo.options.fillOpacity], [2.5, 0.55]);

seed(); m.selectField('A'); m.highlightLot('a1'); m.highlightLot('a2');
const deselectado = m.lotLayers['a1'];
check('deseleccionar dentro del campo activo no lo apaga al base',
      [deselectado.options.weight, deselectado.options.fillOpacity], [2.5, 0.55]);

console.log('\n--- color: el estado sigue mandando ---');
seed(); reset(); addLot('x', 'A', 'Inactive'); m.setGisViewMode('lots');
check('lote inactivo en rojo', m.lotLayers['x'].options.color, '#E74C3C');
m.lotLayers['x'].fire('mouseover');
check('el hover no toca el color', m.lotLayers['x'].options.color, '#E74C3C');

console.log(fails === 0 ? '\nTODO OK\n' : `\n${fails} FALLAS\n`);
process.exit(fails === 0 ? 0 : 1);
