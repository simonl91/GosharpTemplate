package main

import (
	"bytes"
	"fmt"
	"text/template"
	"time"
)

const RUNS = 1000

type TableItem struct {
	Id          int
	Name        string
	Valid       bool
	Description string
	Count       int
	Price       float64
}

func GenerateRange(n int) []TableItem {
	var items = make([]TableItem, n)
	for i := 0; i < n; i++ {
		items[i] = TableItem{
			i,
			fmt.Sprintf("item%d", i),
			true,
			fmt.Sprintf("Description%d", i),
			i % 100,
			100.01,
		}
	}
	return items
}

func main() {
	tmpl, err := template.ParseFiles("List.html")
	if err != nil {
		fmt.Println(err)
		return
	}

	items := GenerateRange(RUNS)

	var times = make([]int64, RUNS)
	for i := 0; i < RUNS; i++ {
		var w bytes.Buffer

		start := time.Now()
		tmpl.ExecuteTemplate(&w, "table", items)
		duration := time.Since(start)
		times[i] = duration.Microseconds()
	}

	var sum int64
	var min int64
	var max int64

	sum = 0
	min = times[0]
	max = 0

	for i := 0; i < RUNS; i++ {
		if times[i] < min {
			min = times[i]
		}
		if times[i] > max {
			max = times[i]
		}
		sum += times[i]
	}

	average := sum / RUNS

	fmt.Printf("Table with 1000 items, %d runs:\n", RUNS)
	fmt.Printf("Min: %d\n", min)
	fmt.Printf("Max: %d\n", max)
	fmt.Printf("Average: %d\n", average)

}
