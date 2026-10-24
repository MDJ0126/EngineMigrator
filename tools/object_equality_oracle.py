"""Independent object identity expectations, not an engine simulator."""
def expected_rows():
 rows=[]
 for left_state in range(4):
  for right_mode in range(5):
   left_null=left_state==0
   right_null=right_mode==0 or(right_mode==1 and left_null)
   left_alive=left_state in (1,3)
   right_alive=left_alive if right_mode==1 else right_mode in (2,4)
   equal=(left_null and right_null)or(left_null and not right_alive)or(right_null and not left_alive)or(right_mode==1)
   rows.append([left_state,right_mode,int(equal),int(not equal),int(not left_alive),int(not right_alive),int(left_alive),int(right_alive),int(equal),int(not equal)])
 return rows

def require_rows(rows,label):
 if type(rows) is not list or len(rows)!=20 or any(type(row) is not list or len(row)!=10 or any(type(value) is not int for value in row)for row in rows):
  raise RuntimeError('Malformed capture: '+label)
 if rows!=expected_rows():raise RuntimeError('Object identity differs from independent oracle: '+label)

def expected_flows():
 return [[r[0],r[1],r[2],r[2],r[2],1,r[2],r[2],1,r[5]]for r in expected_rows()]

def require_flows(rows,label):
 if type(rows)is not list or len(rows)!=20 or any(type(row)is not list or len(row)!=10 or any(type(v)is not int for v in row)for row in rows):raise RuntimeError('Malformed identity flow capture: '+label)
 if rows!=expected_flows():raise RuntimeError('Identity argument/return/conditional/reassignment flow differs: '+label)
