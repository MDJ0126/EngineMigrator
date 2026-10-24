"""Expected Unity Transform identity across owner deletion and activation states."""
def expected_rows():
 rows=[]
 for ls in range(4):
  for mode in range(5):
   la=ls in(1,3);ra=la if mode==1 else mode in(2,4)
   ln=ls==0;rn=mode==0 or(mode==1 and ln)
   eq=(ln and not ra)or(rn and not la)or mode==1
   rows.append([ls,mode,int(eq),int(not eq),int(not la),int(not ra),int(la),int(ra),int(eq),int(not eq),int(la),int(ra),int(eq),int(eq),1,1,int(la),1])
 return rows

def require_rows(rows,label):
 if type(rows)is not list or len(rows)!=20 or any(type(r)is not list or len(r)!=18 or any(type(v)is not int for v in r)for r in rows):raise RuntimeError('Malformed Transform capture: '+label)
 if rows!=expected_rows():raise RuntimeError('Transform identity differs: '+label)
