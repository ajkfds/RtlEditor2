#!/usr/bin/env node
/*
 * Composites the NavigatePanel-style node badges into the tree icons.
 *
 * The desktop editor (VerilogFileNode.GetIcon) draws two overlays on top of the
 * file icon:
 *   - an exclamation triangle at the BOTTOM-LEFT when the parsed document has
 *     errors (red) or warnings (orange);
 *   - a shine at the TOP-RIGHT when the document has unsaved edits.
 *
 * VS Code's `TreeItem.iconPath` can only carry ONE icon and one theme colour,
 * so an overlay cannot be drawn by the client. Instead the overlay is baked
 * into the SVG here: every base icon gets one file per badge
 * (`<icon>-<badge>.svg` / `<icon>-<badge>-dark.svg`).
 *
 * Usage:  node scripts/generate-badge-icons.js
 *
 * The generated files are committed, so running this is only needed after
 * editing a base icon or the badge geometry / colours.
 */

'use strict';

const fs = require('fs');
const path = require('path');

const ICONS_DIR = path.join(__dirname, '..', 'icons');

// Badge colours, matching the Messages pane / Problems panel (and the editor's
// wave mark colours in CodeDrawStyle).
const BADGES = {
    error: { fill: '#f14c4c', topRight: false },
    warning: { fill: '#cca700', topRight: false },
    dirty: { fill: '#ffd24a', topRight: true },
};

// Base icon names the hierarchy server can return (see ResolveItemIcon and
// BuildFileNodes in SystemVerilogLanguageServer).
const BASE_ICONS = [
    'module',
    'interface',
    'package',
    'header',
    'systemverilog-document',
    'verilog-document',
    'verilog-header-document',
];

// Badge geometry inside the 512x512 viewBox: the badge fills the bottom-left /
// top-right quarter, overlapping the base icon's corner so it reads as an
// overlay (the editor uses OverridePosition.DownLeft / UpRight).
const BADGE_START = 288; // 512 - 224
const BADGE_SCALE = 224 / 512;

const EXCLAMATION_PATH =
    'M505.095,407.125L300.77,53.208c-9.206-15.944-26.361-25.849-44.774-25.849' +
    'c-18.412,0-35.552,9.905-44.751,25.849L6.905,407.109c-9.206,15.944-9.206,35.746,0,51.69' +
    'c9.206,15.944,26.354,25.842,44.758,25.842h408.674c18.405,0,35.568-9.897,44.759-25.842' +
    'C514.302,442.855,514.302,423.053,505.095,407.125z M256.004,426.437' +
    'c-17.668,0-32.013-14.33-32.013-32.004c0-17.668,14.345-31.997,32.013-31.997' +
    'c17.667,0,31.997,14.329,31.997,31.997C288.001,412.108,273.671,426.437,256.004,426.437z' +
    'M275.72,324.011c0,10.89-8.834,19.709-19.716,19.709c-10.898,0-19.717-8.818-19.717-19.709' +
    'l-12.296-144.724c0-17.676,14.345-32.005,32.013-32.005c17.667,0,31.997,14.33,31.997,32.005' +
    'L275.72,324.011z';

const SHINE_PATH =
    'M495.469,241.969c-113.594,0-152.875-28.5-174.906-50.531' +
    'c-22.031-22.125-50.578-61.344-50.578-174.922c0-4.328-0.453-16.516-14.016-16.516' +
    'C242.531,0,242,12.188,242,16.516c0,113.578-28.563,152.797-50.594,174.922' +
    'c-22.094,22.031-61.375,50.531-174.906,50.531c-4.344,0-16.5,0.5-16.5,14.047' +
    'c0,13.453,12.156,13.938,16.5,13.938c113.531,0,152.813,28.578,174.906,50.625' +
    'C213.438,342.625,242,381.922,242,495.5c0,4.344,0.531,16.5,13.969,16.5' +
    'c13.563,0,14.016-12.156,14.016-16.5c0-113.578,28.547-152.875,50.578-174.922' +
    'c22.031-22.078,61.313-50.625,174.906-50.625c4.328,0,16.531-0.422,16.531-13.984' +
    'C512,242.516,499.797,241.969,495.469,241.969z';

/** Extracts the <path> elements of a base icon (drops the XML prolog). */
function basePaths(svg) {
    const matches = svg.match(/<path\b[^>]*\/>/g);
    if (!matches) throw new Error('no <path> element found');
    return matches.join('\n    ');
}

/** Wraps the base paths and the badge path into one 16x16 svg. */
function compose(basePathMarkup, fill, topRight) {
    const badgePath = fill === BADGES.dirty.fill ? SHINE_PATH : EXCLAMATION_PATH;
    const x = topRight ? BADGE_START : 0;
    const y = topRight ? 0 : BADGE_START;
    return [
        '<?xml version="1.0" encoding="UTF-8" standalone="no"?>',
        '<svg width="16" height="16" viewBox="0 0 512 512" version="1.1" xmlns="http://www.w3.org/2000/svg">',
        '    ' + basePathMarkup,
        '    <g transform="translate(' + x + ',' + y + ') scale(' + BADGE_SCALE + ')">',
        '        <path d="' + badgePath + '" style="fill:' + fill + ';"/>',
        '    </g>',
        '</svg>',
        '',
    ].join('\n');
}

function main() {
    if (!fs.existsSync(ICONS_DIR)) {
        console.error('icons directory not found: ' + ICONS_DIR);
        process.exit(1);
    }

    let written = 0;
    for (const base of BASE_ICONS) {
        for (const variant of ['', '-dark']) {
            const baseFile = path.join(ICONS_DIR, base + variant + '.svg');
            if (!fs.existsSync(baseFile)) {
                console.warn('  skip (missing base icon): ' + path.basename(baseFile));
                continue;
            }
            const paths = basePaths(fs.readFileSync(baseFile, 'utf8'));
            for (const badgeName of Object.keys(BADGES)) {
                const outFile = path.join(ICONS_DIR, base + '-' + badgeName + variant + '.svg');
                fs.writeFileSync(outFile, compose(paths, BADGES[badgeName].fill, BADGES[badgeName].topRight), 'utf8');
                written++;
            }
        }
    }
    console.log('generated ' + written + ' badge icon(s) in ' + ICONS_DIR);
}

main();
