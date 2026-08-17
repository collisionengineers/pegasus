const DS = window.PegasusDS;
const NAV_ITEMS = [["Dashboard","Dashboard.html"],["Inbox","Inbox.html"],["Upload","Upload.html"],["Queues","Queues.html"],["Cases","Cases.html"],["Operations","Operations.html"],["Administration","Administration.html"]];
const NAV_COUNTS = {Inbox:{n:41}, Queues:{n:22, hot:true}, Cases:{n:124}};
function Shell({nav, children}){
  return <div style={{display:"grid",gridTemplateColumns:"236px minmax(0,1fr)",minHeight:"100vh",background:"var(--paper)"}}>
    <style>{".rail-link{display:flex;align-items:center;justify-content:space-between;gap:8px;padding:8px 10px;border-left:2px solid transparent;color:var(--ink);font-size:var(--t-body);text-decoration:none;min-height:36px}.rail-link:hover{background:var(--panel);color:var(--ink)}.rail-link[aria-current=page]{border-left-color:var(--ce-red);background:var(--ce-red-tint);color:var(--ce-red);font-weight:700}"}</style>
    <aside style={{display:"flex",flexDirection:"column",background:"#fff",borderRight:"var(--border)",position:"sticky",top:0,height:"100vh",overflowY:"auto"}}>
      <a href="Dashboard.html" style={{display:"flex",alignItems:"center",gap:"10px",padding:"12px 14px",borderBottom:"var(--border)",textDecoration:"none"}}>
        <img src={DS.BRAND_LOGO} alt="Collision Engineers" style={{display:"block",width:"88px",height:"28px",objectFit:"contain",objectPosition:"left center"}}/>
        <b style={{fontSize:"17px",fontWeight:800,letterSpacing:"-0.01em",color:"var(--ink)"}}>Pegasus</b>
      </a>
      <nav aria-label="Primary" style={{display:"flex",flexDirection:"column",padding:"10px 8px",gap:"2px"}}>
        {NAV_ITEMS.map(([label,href])=>{
          const c = NAV_COUNTS[label];
          return <a key={label} className="rail-link" href={href} aria-current={label===nav?"page":undefined}>{label}
            {c && <span style={{fontVariantNumeric:"tabular-nums",fontSize:"var(--t-sm)",color:c.hot?"var(--amber-fg)":"var(--muted)",fontWeight:c.hot?700:400}}>{c.n}</span>}
          </a>;
        })}
      </nav>
      <div style={{marginTop:"auto",padding:"12px 14px",borderTop:"var(--border)",display:"flex",flexDirection:"column",gap:"6px"}}>
        <span style={{fontSize:"var(--t-sm)",color:"var(--muted)"}}>alex</span>
        <a href="ChangePassword.html" style={{fontSize:"var(--t-sm)",color:"var(--ink)",textDecoration:"none",fontWeight:600}}>Change password</a>
        <a href="#" style={{fontSize:"var(--t-sm)",color:"var(--muted)",textDecoration:"none"}}>Sign out</a>
      </div>
    </aside>
    <main id="main-content" style={{padding:"20px 24px 32px",maxWidth:"1280px",width:"100%",boxSizing:"border-box"}}>{children}</main>
  </div>;
}
const gbp = (n,dp) => "\u00A3"+n.toLocaleString("en-GB",{minimumFractionDigits:dp==null?2:dp,maximumFractionDigits:dp==null?2:dp});
function Photo({label,h}){
  return <figure style={{margin:0,border:"var(--border)",borderRadius:"var(--radius)",overflow:"hidden",background:"#fff"}}>
    <div style={{height:h||110,display:"grid",placeItems:"center",background:"repeating-linear-gradient(45deg,var(--panel),var(--panel) 12px,#fff 12px,#fff 24px)",color:"var(--muted)"}}><DS.Icon name="file-text"/></div>
    <figcaption style={{padding:"6px 10px",fontSize:"var(--t-xs)",color:"var(--muted)",borderTop:"var(--border)",whiteSpace:"nowrap",overflow:"hidden",textOverflow:"ellipsis"}}>{label}</figcaption>
  </figure>;
}
function Head({term,val,quiet}){
  return <span style={{display:"inline-grid",gap:"1px",paddingLeft:"14px",borderLeft:"1px solid rgba(255,255,255,.18)"}}>
    <small style={{fontSize:"10px",letterSpacing:".1em",textTransform:"uppercase",color:"rgba(255,255,255,.55)"}}>{term}</small>
    <b style={{fontSize:"var(--t-sm)",fontWeight:600,color:quiet?"rgba(255,255,255,.6)":"#fff"}}>{val}</b></span>;
}
function mount(el){ ReactDOM.createRoot(document.getElementById("root")).render(el); }
Object.assign(window,{DS,Shell,gbp,Photo,Head,mount});
