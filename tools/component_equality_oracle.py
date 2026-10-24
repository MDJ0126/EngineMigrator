"""Component identity is independent of owner identity and owner activation."""
def cases():return[(ls,mode)for ls in range(5)for mode in range(7)if(ls,mode)!=(0,6)]

def expected_rows():
 rows=[]
 for ls,mode in cases():
  la=ls in(1,3);lo=ls in(1,2,3)
  ra=la if mode==1 else(ls!=4 if mode==6 else mode in(2,4))
  ro=lo if mode in(1,6)else mode in(2,3,4)
  ln=ls==0;rn=mode==0 or(mode==1 and ln)
  eq=(ln and not ra)or(rn and not la)or mode==1
  rows.append([ls,mode,int(eq),int(not eq),int(not la),int(not ra),int(la),int(ra),int(eq),int(not eq),int(lo),int(ro),int(eq),int(eq),1,1])
 return rows

def require_rows(rows,label):
 if type(rows)is not list or len(rows)!=34 or any(type(r)is not list or len(r)!=16 or any(type(v)is not int for v in r)for r in rows):raise RuntimeError('Malformed component capture: '+label)
 if rows!=expected_rows():raise RuntimeError('Component/owner identity differs: '+label)
