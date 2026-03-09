k = 0
t3 = [3**i for i in range(1,20)]
for n in range(200000,999999):
    if k == 5:
        break
    for i in range(len(t3)):
        if t3[i] > n:
            break
        if (n - t3[i]) % 103 == 0 and (n - t3[i]) % 2 != 0 and "1" not in str(n):
            print(n, i+1)
            k += 1
            break