export const designs = [
  {id:'a',name:'Review desk',lead:'A clear decision, with every file alongside.',benefit:'The strongest everyday balance. Case identity and the complete file roster stay together in one work surface.',tradeoff:'Less photographic context until a file is opened.',recommended:true},
  {id:'b',name:'Guided review',lead:'One calm, continuous path from files to Case.',benefit:'A centred review sheet, a compact visual file summary and an explicit three-stage journey make occasional uploads easy to follow.',tradeoff:'Inspecting the complete roster takes one disclosure.'},
  {id:'c',name:'Contact sheet',lead:'Recognise the evidence before choosing its home.',benefit:'Large, real image previews make a photographic upload easy to recognise. A steady Case panel stays beside the gallery.',tradeoff:'Long image sets need more vertical space than the ledger.'},
  {id:'d',name:'Compact ledger',lead:'The whole upload, in a single scan.',benefit:'A horizontal Case decision above precise, compact file rows. Best for large batches, mixed formats and repeated office use.',tradeoff:'Small thumbnails provide less image detail without opening a preview.'},
  {id:'e',name:'Inspection studio',lead:'Look closely. Keep the Case in view.',benefit:'A large photograph, a selectable file rail and the Case decision form a dedicated review workspace.',tradeoff:'Uses the most screen space; narrower windows stack the photograph below the decision.'},
];
export const states = [
 ['select','Choose files'],['chosen','Files selected'],['uploading','Uploading'],['processing','Stored · processing'],
 ['ready','One possible Case'],['multiple','Several possible Cases'],['no-match','No matching Case'],['search-error','Case search unavailable'],
 ['mixed','One unreadable file'],['unreadable','All files unreadable'],['upload-error','Upload failed'],['conflict','Confirmation conflict'],
 ['single','Single image'],['document','Instruction document'],['duplicate','Already received'],['attached','Added to Case'],
 ['discarded','Discarded'],['incomplete','Incomplete upload'],['confirm','Review exact destination'],['discard','Confirm discard']
];
export const cases=[
 {ref:'QDOS26010',po:'PO-48271',reg:'BH17RZV',claimant:'J. Morgan',principal:'Northbridge Insurance',stage:'Review',tone:'navy'},
 {ref:'QDOS25984',po:'PO-47908',reg:'BH17RZV',claimant:'A. Taylor',principal:'Cedar Motor Claims',stage:'With Engineer',tone:'blue'},
 {ref:'QDOS25802',po:'PO-46112',reg:'LM21WKT',claimant:'R. Lewis',principal:'Northbridge Insurance',stage:'Complete',tone:'green'},
 {ref:'QDOS26018',po:'PO-48306',reg:'BH17RZV',claimant:'J. Morgan',principal:'Northbridge Insurance',stage:'Triage',tone:'green'},
];
export const files=Array.from({length:11},(_,i)=>({id:'f'+i,name:`WhatsApp Image 2026-09-17 at 12.57.${i<6?'41':'42'} PM${i?' ('+i+')':''}.jpeg`,size:Math.round((0.23+i*.01)*1048576),kind:'JPEG image',tile:i%6}));
