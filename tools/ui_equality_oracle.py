"""Unity Canvas/Button identity: distinct component kinds share an owner but not identity."""
def expected_rows():
 rows=[]
 for ls in range(5):
  for mode in range(6):
   la=ls in(1,3);lo=ls in(1,2,3)
   ra=la if mode==1 else mode in(2,4);ro=lo if mode==1 else mode in(2,3,4)
   ln=ls==0;rn=mode==0 or(mode==1 and ln)
   eq=(ln and not ra)or(rn and not la)or mode==1
   beq=(ln and not ro)or(rn and not lo)or mode==1
   rows.append([ls,mode,int(eq),int(not eq),int(not la),int(not ra),int(la),int(ra),int(beq),int(lo),int(ro),int(ln),int(rn),int(lo),int(ro),int(eq),int(eq),1])
 return rows

def require_rows(rows,label):
 if type(rows)is not list or len(rows)!=30 or any(type(r)is not list or len(r)!=18 or any(type(v)is not int for v in r)for r in rows):raise RuntimeError('Malformed UI identity capture: '+label)
 if rows!=expected_rows():raise RuntimeError('UI kind/liveness/identity differs: '+label)
