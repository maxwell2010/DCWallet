/** Local SVG geometry follows the DW 24-unit stroke icon family. No remote assets. */
const drawings={
 assets:['M5 5h14a2 2 0 0 1 2 2v12H5a3 3 0 0 1-3-3V6a3 3 0 0 1 3-3h12','M2 8h19M16 12h5v4h-5z','M16 14h.01'],
 send:['M12 21V7m-5 5 5-5 5 5M5 3h14'],
 receive:['M12 3v14m-5-5 5 5 5-5M5 21h14'],
 swap:['M4 8h15m-4-4 4 4-4 4M20 16H5m4-4-4 4 4 4'],
 stake:['m12 3 9 5-9 5-9-5 9-5z','m3 12 9 5 9-5M3 16l9 5 9-5'],
 history:['M3 11a9 9 0 1 1 2 7','M3 4v7h7M12 7v5l3 2'],
 settings:['M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8','m9 3 .8 2h4.4L15 3l3 1.7-.8 2 2.2 3.8 2.1.3v3.4l-2.1.3-2.2 3.8.8 2-3 1.7-.8-2H9.8L9 22l-3-1.7.8-2-2.2-3.8-2.1-.3v-3.4l2.1-.3 2.2-3.8-.8-2L9 3z'],
 refresh:['M20 10a8 8 0 0 0-14-5L3 8M3 3v5h5','M4 14a8 8 0 0 0 14 5l3-3M21 21v-5h-5'],
 lock:['M6 10h12v11H6z','M8 10V7a4 4 0 0 1 8 0v3M12 15v2'],
 unlock:['M6 10h12v11H6z','M8 10V7a4 4 0 0 1 7.5-2M12 15v2'],
 export:['M12 3v12m-4-4 4 4 4-4','M4 15v6h16v-6'],
 restore:['M4 8V3m0 5h5','M4 8a8 8 0 1 1-1 7M12 8v4l3 2'],
 create:['M12 5v14M5 12h14'],
 check:['m5 12 4 4L19 6'],
 shield:['m12 3 8 3v6c0 4-4 7-8 9-4-2-8-5-8-9V6l8-3z','m8 12 3 3 5-6'],
 copy:['M9 8h11v13H9z','M15 4V2H3v14h2'],
 sun:['M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8','M12 2v2m0 16v2M2 12h2m16 0h2M5 5l1.5 1.5m11 11L19 19M5 19l1.5-1.5m11-11L19 5'],
 moon:['M20 14.3A8.5 8.5 0 0 1 9.7 4a8.5 8.5 0 1 0 10.3 10.3z'],
} as const;
type IconName=keyof typeof drawings;
function icon(name:IconName){
 const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');
 svg.setAttribute('viewBox','0 0 24 24');svg.setAttribute('aria-hidden','true');svg.setAttribute('focusable','false');svg.classList.add('mobile-icon');
 for(const geometry of drawings[name]){const path=document.createElementNS('http://www.w3.org/2000/svg','path');path.setAttribute('d',geometry);svg.append(path);}
 return svg;
}
function decorate(button:HTMLElement,name:IconName){
 button.querySelector(':scope > .mobile-icon')?.remove();button.prepend(icon(name));button.classList.add('button-with-icon');
}
/** Decoration only: retains native buttons, accessible labels, state and all click handlers. */
export function initMobileIcons(){
 for(const button of document.querySelectorAll<HTMLButtonElement>('.wallet-nav [data-mobile-view]')){
  const route=button.dataset.mobileView as IconName;if(!(route in drawings))continue;
  button.querySelector(':scope > svg, :scope > [aria-hidden="true"]')?.remove();button.prepend(icon(route));
 }
 const buttons:Record<string,IconName>={
  'refresh-assets':'refresh','refresh-history':'refresh','refresh-pending':'refresh','stake-refresh':'refresh',
  'send-preview':'send','swap-preview':'swap','stake-preview':'stake','tstake-prepare':'stake',
  'create':'create','restore-seed':'restore','import':'restore','unlock':'unlock','lock':'lock','export':'export',
  'saved':'check','verify-backup':'shield','send-confirm-button':'check','advanced-approve':'check','tstake-confirm':'check',
 };
 for(const [id,name]of Object.entries(buttons)){const button=document.getElementById(id);if(!button)continue;
  if(id==='refresh-history'){button.replaceChildren(icon(name));button.classList.add('icon-only');}else decorate(button,name);
 }
 for(const button of document.querySelectorAll<HTMLElement>('[data-mobile-icon]')){const name=button.dataset.mobileIcon as IconName;if(name in drawings)decorate(button,name);}
 const theme=document.getElementById('theme');if(theme){const sun=icon('sun'),moon=icon('moon');sun.classList.add('theme-sun');moon.classList.add('theme-moon');theme.replaceChildren(sun,moon);}
}
